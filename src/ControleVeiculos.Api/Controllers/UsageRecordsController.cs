using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Api.Auth;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Domain.Enums;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.UsageRecords;

namespace ControleVeiculos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsageRecordsController(
    IUsageRecordRepository usageRepository,
    IVehicleRepository vehicleRepository,
    IDriverRepository driverRepository,
    IVoiceTranscriptionService transcriptionService,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UsageRecordDto>>> List(CancellationToken ct)
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Gestor))
            return Ok((await usageRepository.ListAsync(ct)).Select(ToDto));

        var driver = await GetCurrentDriverAsync(ct);
        if (driver is null)
            return Ok(Array.Empty<UsageRecordDto>());

        return Ok((await usageRepository.ListByMotoristaAsync(driver.Id, ct)).Select(ToDto));
    }

    [HttpGet("atual")]
    [Authorize(Roles = Roles.Motorista)]
    public async Task<ActionResult<UsageRecordDto>> GetEmAndamento(CancellationToken ct)
    {
        var driver = await GetCurrentDriverAsync(ct);
        if (driver is null)
            return NotFound("Usuário não está associado a um motorista.");

        var usage = await usageRepository.GetEmAndamentoByMotoristaAsync(driver.Id, ct);
        return usage is null ? NotFound() : Ok(ToDto(usage));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UsageRecordDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var usage = await usageRepository.GetWithDetailsAsync(id, ct);
        if (usage is null || !await CanAccessAsync(usage, ct))
            return NotFound();

        return Ok(ToDetailDto(usage));
    }

    [HttpPost("iniciar")]
    [Authorize(Roles = Roles.Motorista)]
    public async Task<ActionResult<UsageRecordDto>> Start(StartUsageRequest request, CancellationToken ct)
    {
        var driver = await GetCurrentDriverAsync(ct);
        if (driver is null)
            return BadRequest("Usuário não está associado a um motorista.");

        if (await usageRepository.GetEmAndamentoByMotoristaAsync(driver.Id, ct) is not null)
            return BadRequest("Já existe um uso em andamento para este motorista.");

        var vehicle = await vehicleRepository.GetByIdAsync(request.VeiculoId, ct);
        if (vehicle is null)
            return BadRequest("Veículo não encontrado.");
        if (vehicle.Status != VehicleStatus.Disponivel)
            return BadRequest("Veículo não está disponível.");

        var usage = new UsageRecord
        {
            VeiculoId = vehicle.Id,
            MotoristaId = driver.Id,
            Finalidade = request.Finalidade,
            Origem = request.Origem,
            Destino = request.Destino,
            OdometroInicial = request.OdometroInicial,
        };

        vehicle.Status = VehicleStatus.EmUso;
        vehicleRepository.Update(vehicle);

        await usageRepository.AddAsync(usage, ct);
        await usageRepository.SaveChangesAsync(ct);

        usage.Veiculo = vehicle;
        usage.Motorista = driver;
        return CreatedAtAction(nameof(GetById), new { id = usage.Id }, ToDto(usage));
    }

    [HttpPost("{id:guid}/finalizar")]
    [Authorize(Roles = Roles.Motorista)]
    public async Task<IActionResult> Finish(Guid id, FinishUsageRequest request, CancellationToken ct)
    {
        var usage = await usageRepository.GetWithDetailsAsync(id, ct);
        if (usage is null || !await CanAccessAsync(usage, ct))
            return NotFound();
        if (usage.Status != UsageRecordStatus.EmAndamento)
            return BadRequest("Este uso já foi finalizado.");
        if (request.OdometroFinal < usage.OdometroInicial)
            return BadRequest("Odômetro final não pode ser menor que o inicial.");

        usage.OdometroFinal = request.OdometroFinal;
        usage.FinalizadoEm = DateTimeOffset.UtcNow;
        usage.Status = UsageRecordStatus.Finalizado;
        usage.UpdatedAt = DateTimeOffset.UtcNow;
        usageRepository.Update(usage);

        var vehicle = await vehicleRepository.GetByIdAsync(usage.VeiculoId, ct);
        if (vehicle is not null)
        {
            vehicle.OdometroAtual = request.OdometroFinal;
            vehicle.Status = VehicleStatus.Disponivel;
            vehicleRepository.Update(vehicle);
        }

        await usageRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/fotos")]
    public async Task<ActionResult<VehiclePhotoDto>> UploadPhoto(Guid id, IFormFile file, [FromForm] VehiclePhotoType tipo, [FromForm] string? observacao, CancellationToken ct)
    {
        var usage = await usageRepository.GetWithDetailsAsync(id, ct);
        if (usage is null || !await CanAccessAsync(usage, ct))
            return NotFound();
        if (file.Length == 0)
            return BadRequest("Arquivo vazio.");

        var fullPath = await SaveFileAsync(file, "fotos", id, ct);

        var photo = new VehiclePhoto
        {
            UsoId = id,
            Tipo = tipo,
            ArquivoUrl = fullPath,
            Observacao = observacao,
        };

        await usageRepository.AddPhotoAsync(photo, ct);
        await usageRepository.SaveChangesAsync(ct);

        return Ok(new VehiclePhotoDto(photo.Id, photo.Tipo, photo.ArquivoUrl, photo.Observacao, photo.CreatedAt));
    }

    [HttpPost("{id:guid}/notas-de-voz")]
    public async Task<ActionResult<VoiceNoteDto>> UploadVoiceNote(Guid id, IFormFile file, CancellationToken ct)
    {
        var usage = await usageRepository.GetWithDetailsAsync(id, ct);
        if (usage is null || !await CanAccessAsync(usage, ct))
            return NotFound();
        if (file.Length == 0)
            return BadRequest("Arquivo vazio.");

        var fullPath = await SaveFileAsync(file, "notas-de-voz", id, ct);

        var note = new VoiceNote { UsoId = id, ArquivoUrl = fullPath };

        await using (var stream = System.IO.File.OpenRead(fullPath))
        {
            var transcricao = await transcriptionService.TranscribeAsync(stream, file.ContentType, ct);
            if (transcricao is not null)
            {
                note.TranscricaoTexto = transcricao;
                note.Status = VoiceNoteStatus.Transcrito;
            }
            else
            {
                note.Status = VoiceNoteStatus.Falhou;
            }
        }

        await usageRepository.AddVoiceNoteAsync(note, ct);
        await usageRepository.SaveChangesAsync(ct);

        return Ok(new VoiceNoteDto(note.Id, note.ArquivoUrl, note.TranscricaoTexto, note.Status, note.CreatedAt));
    }

    [HttpPost("{id:guid}/abastecimentos")]
    public async Task<ActionResult<FuelEntryDto>> AddFuelEntry(Guid id, AddFuelEntryRequest request, CancellationToken ct)
    {
        var usage = await usageRepository.GetWithDetailsAsync(id, ct);
        if (usage is null || !await CanAccessAsync(usage, ct))
            return NotFound();

        var entry = new FuelEntry
        {
            UsoId = id,
            Litros = request.Litros,
            ValorTotal = request.ValorTotal,
            Odometro = request.Odometro,
        };

        await usageRepository.AddFuelEntryAsync(entry, ct);
        await usageRepository.SaveChangesAsync(ct);

        return Ok(new FuelEntryDto(entry.Id, entry.Litros, entry.ValorTotal, entry.Odometro, entry.CreatedAt));
    }

    private async Task<string> SaveFileAsync(IFormFile file, string subpasta, Guid usageId, CancellationToken ct)
    {
        var basePath = Path.Combine(
            environment.ContentRootPath,
            configuration["Storage:UsageRecordsPath"] ?? "App_Data/usage-records",
            usageId.ToString(),
            subpasta);
        Directory.CreateDirectory(basePath);

        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(basePath, storedFileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream, ct);

        return fullPath;
    }

    private async Task<Driver?> GetCurrentDriverAsync(CancellationToken ct) =>
        await driverRepository.GetByUserIdAsync(User.GetUserId(), ct);

    private async Task<bool> CanAccessAsync(UsageRecord usage, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Gestor))
            return true;

        var driver = await GetCurrentDriverAsync(ct);
        return driver is not null && usage.MotoristaId == driver.Id;
    }

    private static UsageRecordDto ToDto(UsageRecord u) => new(
        u.Id, u.VeiculoId, u.Veiculo?.Placa ?? "?", u.MotoristaId, u.Motorista?.Nome ?? "?",
        u.Finalidade, u.Origem, u.Destino, u.OdometroInicial, u.OdometroFinal,
        u.IniciadoEm, u.FinalizadoEm, u.Status);

    private static UsageRecordDetailDto ToDetailDto(UsageRecord u) => new(
        u.Id, u.VeiculoId, u.Veiculo?.Placa ?? "?", u.MotoristaId, u.Motorista?.Nome ?? "?",
        u.Finalidade, u.Origem, u.Destino, u.OdometroInicial, u.OdometroFinal,
        u.IniciadoEm, u.FinalizadoEm, u.Status,
        u.Fotos.Select(f => new VehiclePhotoDto(f.Id, f.Tipo, f.ArquivoUrl, f.Observacao, f.CreatedAt)).ToList(),
        u.NotasDeVoz.Select(n => new VoiceNoteDto(n.Id, n.ArquivoUrl, n.TranscricaoTexto, n.Status, n.CreatedAt)).ToList(),
        u.Abastecimentos.Select(a => new FuelEntryDto(a.Id, a.Litros, a.ValorTotal, a.Odometro, a.CreatedAt)).ToList());
}
