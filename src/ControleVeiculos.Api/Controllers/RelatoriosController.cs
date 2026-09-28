using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ControleVeiculos.Api.Relatorios;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Relatorios;

namespace ControleVeiculos.Api.Controllers;

/// <summary>Relatórios gerenciais (só administração). Período em datas de Brasília; sem período = mês atual.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
[Route("api/[controller]")]
public class RelatoriosController(
    IUsageRecordRepository usageRepository,
    IParametrosCustoRepository parametrosRepository,
    IOptions<BaseOperacional> baseOperacional) : ControllerBase
{
    private const int DiasMaximos = 366 * 2;

    /// <summary>Janela usada pra sugerir a tarifa (custo recente do km).</summary>
    private const int DiasCustoRecente = 90;

    [HttpGet]
    public async Task<ActionResult<RelatorioDto>> Get([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        if (Periodo(de, ate) is not { } periodo)
            return BadRequest($"Período inválido (a data inicial deve ser antes da final, no máximo {DiasMaximos} dias).");

        var (relatorio, _, _) = await MontarAsync(periodo.De, periodo.Ate, ct);
        return Ok(relatorio);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> Excel([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        if (Periodo(de, ate) is not { } periodo)
            return BadRequest($"Período inválido (a data inicial deve ser antes da final, no máximo {DiasMaximos} dias).");

        var (relatorio, saidas, parametros) = await MontarAsync(periodo.De, periodo.Ate, ct);
        var arquivo = RelatorioExcel.Gerar(relatorio, saidas, parametros);
        return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"relatorio-veiculos-{periodo.De:yyyy-MM-dd}-a-{periodo.Ate:yyyy-MM-dd}.xlsx");
    }

    /// <summary>Tarifa, pneus e manutenção + o custo real do km nos últimos 90 dias (pra revisar a tarifa).</summary>
    [HttpGet("parametros")]
    public async Task<ActionResult<ParametrosCustoDto>> GetParametros(CancellationToken ct)
    {
        var parametros = await parametrosRepository.ObterAsync(ct);
        var hoje = RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow);
        var de = hoje.AddDays(-(DiasCustoRecente - 1));
        var carregadas = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.CarregarDesde(de, hoje), ct);
        var ciclos = RelatorioCalculadora.Ciclos(carregadas.Where(u => u.Status != Domain.Enums.UsageRecordStatus.Cancelado).ToList());
        var (combustivel, _, kmPorLitro) = RelatorioCalculadora.CombustivelPorKm(ciclos, de, hoje);
        return Ok(ParaDto(parametros, combustivel, kmPorLitro));
    }

    [HttpPut("parametros")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ParametrosCustoDto>> SalvarParametros(ParametrosCustoDto request, CancellationToken ct)
    {
        if (request.TarifaPorKm < 0 || request.PrecoJogoPneus < 0 || request.ManutencaoPorKm < 0 || request.VidaUtilPneusKm <= 0)
            return BadRequest("Confira os valores: nenhum pode ser negativo e a vida útil dos pneus precisa ser maior que zero.");

        await parametrosRepository.SalvarAsync(new ParametrosCusto
        {
            TarifaPorKm = Math.Round(request.TarifaPorKm, 4),
            PrecoJogoPneus = Math.Round(request.PrecoJogoPneus, 2),
            VidaUtilPneusKm = request.VidaUtilPneusKm,
            ManutencaoPorKm = Math.Round(request.ManutencaoPorKm, 4),
        }, ct);
        return await GetParametros(ct);
    }

    private static ParametrosCustoDto ParaDto(ParametrosCusto p, decimal? combustivelKm, decimal? kmPorLitro) => new(
        p.TarifaPorKm, p.PrecoJogoPneus, p.VidaUtilPneusKm, p.ManutencaoPorKm,
        Math.Round(p.PneusPorKm, 4), Math.Round(p.ExtraPorKm, 4),
        combustivelKm, combustivelKm is { } c ? Math.Round(c + p.ExtraPorKm, 4) : null, kmPorLitro);

    private static (DateOnly De, DateOnly Ate)? Periodo(DateOnly? de, DateOnly? ate)
    {
        var hoje = RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow);
        var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = ate ?? hoje;
        if (fim < inicio || fim.DayNumber - inicio.DayNumber > DiasMaximos)
            return null;
        return (inicio, fim);
    }

    private async Task<(RelatorioDto Relatorio, List<UsageRecord> SaidasDoPeriodo, ParametrosCusto Parametros)> MontarAsync(
        DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var parametros = await parametrosRepository.ObterAsync(ct);
        var carregadas = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.CarregarDesde(de, ate), ct);
        var relatorio = RelatorioCalculadora.Calcular(carregadas, de, ate, baseOperacional.Value, parametros, DateTimeOffset.UtcNow);
        var doPeriodo = carregadas.Where(u => RelatorioCalculadora.NoPeriodo(u, de, ate)).ToList();
        return (relatorio, doPeriodo, parametros);
    }
}
