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
    IOdometerOcrService odometerOcrService,
    IFuelReceiptOcrService fuelReceiptOcrService,
    IGeocodingService geocodingService,
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

        // Origem já resolvida na tela (leitura do painel) chega pronta; se só vierem as
        // coordenadas (cliente sem essa etapa), resolve aqui como fallback.
        var origem = request.Origem;
        if (string.IsNullOrWhiteSpace(origem) && request.Latitude is { } lat && request.Longitude is { } lon)
            origem = await geocodingService.ReverseGeocodeAsync(lat, lon, ct);

        var usage = new UsageRecord
        {
            VeiculoId = vehicle.Id,
            MotoristaId = driver.Id,
            Finalidade = request.Finalidade,
            Origem = origem,
            Destino = request.Destino,
            OdometroInicial = request.OdometroInicial,
            LatitudeInicial = request.Latitude,
            LongitudeInicial = request.Longitude,
        };

        vehicle.Status = VehicleStatus.EmUso;
        vehicleRepository.Update(vehicle);

        await usageRepository.AddAsync(usage, ct);
        await usageRepository.SaveChangesAsync(ct);

        usage.Veiculo = vehicle;
        usage.Motorista = driver;
        return CreatedAtAction(nameof(GetById), new { id = usage.Id }, ToDto(usage));
    }

    /// <summary>
    /// Lê uma foto do painel do veículo (odômetro) e, se coordenadas forem enviadas, resolve o
    /// endereço por geocodificação reversa — usado pra pré-preencher o formulário de "iniciar
    /// uso" antes de o registro existir (por isso não fica embaixo de {id:guid}). O motorista
    /// ainda confirma os valores antes de enviar o POST /iniciar de verdade.
    /// </summary>
    [HttpPost("ler-painel")]
    [Authorize(Roles = Roles.Motorista)]
    public async Task<ActionResult<PainelReadingDto>> LerPainel(IFormFile file, [FromForm] double? latitude, [FromForm] double? longitude, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("Arquivo vazio.");

        int? odometro;
        await using (var stream = file.OpenReadStream())
            odometro = await odometerOcrService.ExtractOdometerAsync(stream, ct);

        string? endereco = null;
        if (latitude is { } lat && longitude is { } lon)
            endereco = await geocodingService.ReverseGeocodeAsync(lat, lon, ct);

        return Ok(new PainelReadingDto(odometro, endereco));
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

        if (tipo is VehiclePhotoType.OdometroInicial or VehiclePhotoType.OdometroFinal)
        {
            await using var stream = System.IO.File.OpenRead(fullPath);
            photo.OdometroLido = await odometerOcrService.ExtractOdometerAsync(stream, ct);
        }

        await usageRepository.AddPhotoAsync(photo, ct);
        await usageRepository.SaveChangesAsync(ct);

        return Ok(new VehiclePhotoDto(photo.Id, photo.Tipo, photo.ArquivoUrl, photo.Observacao, photo.OdometroLido, photo.CreatedAt));
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
            ValorPorLitro = request.ValorPorLitro,
        };

        await usageRepository.AddFuelEntryAsync(entry, ct);
        await usageRepository.SaveChangesAsync(ct);

        return Ok(new FuelEntryDto(entry.Id, entry.Litros, entry.ValorTotal, entry.ValorPorLitro, entry.Odometro, entry.CreatedAt));
    }

    /// <summary>
    /// Lê uma foto de nota fiscal/cupom de abastecimento e sugere litros/valor total/valor por
    /// litro. Também salva a foto (tipo ComprovanteAbastecimento) pra auditoria, mesmo que a
    /// leitura falhe. O motorista confirma os valores antes de enviar o POST /abastecimentos.
    /// </summary>
    [HttpPost("{id:guid}/abastecimentos/ler-comprovante")]
    public async Task<ActionResult<FuelReceiptReadingDto>> LerComprovante(Guid id, IFormFile file, CancellationToken ct)
    {
        var usage = await usageRepository.GetWithDetailsAsync(id, ct);
        if (usage is null || !await CanAccessAsync(usage, ct))
            return NotFound();
        if (file.Length == 0)
            return BadRequest("Arquivo vazio.");

        var fullPath = await SaveFileAsync(file, "fotos", id, ct);

        FuelReceiptReading leitura;
        await using (var stream = System.IO.File.OpenRead(fullPath))
            leitura = await fuelReceiptOcrService.ExtractAsync(stream, ct);

        var photo = new VehiclePhoto
        {
            UsoId = id,
            Tipo = VehiclePhotoType.ComprovanteAbastecimento,
            ArquivoUrl = fullPath,
        };
        await usageRepository.AddPhotoAsync(photo, ct);
        await usageRepository.SaveChangesAsync(ct);

        return Ok(new FuelReceiptReadingDto(leitura.Litros, leitura.ValorTotal, leitura.ValorPorLitro));
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
        u.Fotos.Select(f => new VehiclePhotoDto(f.Id, f.Tipo, f.ArquivoUrl, f.Observacao, f.OdometroLido, f.CreatedAt)).ToList(),
        u.NotasDeVoz.Select(n => new VoiceNoteDto(n.Id, n.ArquivoUrl, n.TranscricaoTexto, n.Status, n.CreatedAt)).ToList(),
        u.Abastecimentos.Select(a => new FuelEntryDto(a.Id, a.Litros, a.ValorTotal, a.ValorPorLitro, a.Odometro, a.CreatedAt)).ToList());
}
