using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Api.Relatorios;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Manutencoes;

namespace ControleVeiculos.Api.Controllers;

/// <summary>Manutenções dos veículos (administração): histórico, custos e próximas (vencida / em breve).</summary>
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
[Route("api/[controller]")]
public class ManutencoesController(
    IManutencaoRepository manutencaoRepository,
    IVehicleRepository vehicleRepository,
    IUsageRecordRepository usageRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ManutencaoDto>>> Listar([FromQuery] Guid? veiculoId, CancellationToken ct) =>
        Ok((await manutencaoRepository.ListarAsync(veiculoId, ct)).Select(ToDto));

    /// <summary>Resumo por veículo: km atual, gasto e custo por km (12 meses), próximas manutenções.</summary>
    [HttpGet("resumo")]
    public async Task<ActionResult<IEnumerable<ManutencaoResumoDto>>> Resumo(CancellationToken ct)
    {
        var hoje = RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow);
        var veiculos = await vehicleRepository.ListAsync(ct);
        var manutencoes = await manutencaoRepository.ListarAsync(null, ct);
        var usos = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.InicioDoDia(hoje.AddYears(-1)), ct);
        return Ok(veiculos.OrderBy(v => v.Placa).Select(v => ManutencaoCalculo.Resumo(v, manutencoes, usos, hoje)));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ManutencaoDto>> Criar(SalvarManutencaoRequest request, CancellationToken ct)
    {
        if (await Validar(request, ct) is { } erro)
            return BadRequest(erro);

        var manutencao = new Manutencao { VeiculoId = request.VeiculoId, Tipo = "" };
        Aplicar(manutencao, request);
        await manutencaoRepository.AddAsync(manutencao, ct);
        await AtualizarKmDoVeiculoAsync(request, ct);
        await manutencaoRepository.SaveChangesAsync(ct);
        return Ok(ToDto(manutencao));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ManutencaoDto>> Editar(Guid id, SalvarManutencaoRequest request, CancellationToken ct)
    {
        var manutencao = await manutencaoRepository.GetByIdAsync(id, ct);
        if (manutencao is null)
            return NotFound();
        if (await Validar(request, ct) is { } erro)
            return BadRequest(erro);

        manutencao.VeiculoId = request.VeiculoId;
        Aplicar(manutencao, request);
        manutencao.UpdatedAt = DateTimeOffset.UtcNow;
        await AtualizarKmDoVeiculoAsync(request, ct);
        await manutencaoRepository.SaveChangesAsync(ct);
        return Ok(ToDto(manutencao));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var manutencao = await manutencaoRepository.GetByIdAsync(id, ct);
        if (manutencao is null)
            return NotFound();
        manutencaoRepository.Remove(manutencao);
        await manutencaoRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> Validar(SalvarManutencaoRequest r, CancellationToken ct)
    {
        if (await vehicleRepository.GetByIdAsync(r.VeiculoId, ct) is null)
            return "Veículo não encontrado.";
        if (string.IsNullOrWhiteSpace(r.Tipo))
            return "Escolha o tipo da manutenção.";
        if (r.Km <= 0)
            return "Informe o km do carro na manutenção.";
        if (r.Valor < 0)
            return "O valor não pode ser negativo.";
        if (r.ProximaKm is { } pk && pk <= r.Km)
            return "A próxima manutenção precisa ser num km maior que o atual.";
        if (r.ProximaData is { } pd && pd <= r.Data)
            return "A data da próxima manutenção precisa ser depois desta.";
        if (r.Data > RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow).AddDays(1))
            return "A data não pode ser no futuro.";
        return null;
    }

    private static void Aplicar(Manutencao m, SalvarManutencaoRequest r)
    {
        m.Data = r.Data;
        m.Km = r.Km;
        m.Tipo = MotivoUso.Normalizar(r.Tipo);
        m.Descricao = string.IsNullOrWhiteSpace(r.Descricao) ? null : r.Descricao.Trim();
        m.Valor = Math.Round(r.Valor, 2);
        m.Oficina = string.IsNullOrWhiteSpace(r.Oficina) ? null : MotivoUso.Normalizar(r.Oficina);
        m.ProximaKm = r.ProximaKm;
        m.ProximaData = r.ProximaData;
    }

    /// <summary>Km da manutenção maior que o km conhecido do carro → atualiza (referência pras próximas leituras).</summary>
    private async Task AtualizarKmDoVeiculoAsync(SalvarManutencaoRequest r, CancellationToken ct)
    {
        var veiculo = await vehicleRepository.GetByIdAsync(r.VeiculoId, ct);
        if (veiculo is not null && r.Km > veiculo.OdometroAtual)
        {
            veiculo.OdometroAtual = r.Km;
            vehicleRepository.Update(veiculo);
        }
    }

    private static ManutencaoDto ToDto(Manutencao m) => new(
        m.Id, m.VeiculoId, m.Veiculo?.Placa ?? "", m.Data, m.Km, m.Tipo, m.Descricao, m.Valor, m.Oficina, m.ProximaKm, m.ProximaData);
}
