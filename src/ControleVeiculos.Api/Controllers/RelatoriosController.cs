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
    IManutencaoRepository manutencaoRepository,
    IVehicleRepository vehicleRepository,
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

        var (relatorio, _, _, _) = await MontarAsync(periodo.De, periodo.Ate, ct);
        return Ok(relatorio);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> Excel([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        if (Periodo(de, ate) is not { } periodo)
            return BadRequest($"Período inválido (a data inicial deve ser antes da final, no máximo {DiasMaximos} dias).");

        var (relatorio, saidas, parametros, diaZero) = await MontarAsync(periodo.De, periodo.Ate, ct);
        var arquivo = RelatorioExcel.Gerar(relatorio, saidas, parametros, diaZero);
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
        // Manutenção real (sem pneus, que têm custo próprio) nos últimos 12 meses ÷ km rodados no período.
        var usos12 = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.InicioDoDia(hoje.AddYears(-1)), ct);
        var manutencoes = await manutencaoRepository.ListarAsync(null, ct);
        var resumos = (await vehicleRepository.ListAsync(ct)).Select(v => ManutencaoCalculo.Resumo(v, manutencoes, usos12, hoje)).ToList();
        var km12 = resumos.Sum(r => r.KmRodados12Meses);
        var manutencaoSemPneus = manutencoes
            .Where(m => m.Data > hoje.AddYears(-1))
            .Sum(ManutencaoCalculo.GastoSemPneus);
        return Ok(ParaDto(parametros, combustivel, kmPorLitro) with
        {
            ManutencaoPorKmReal = km12 > 0 && manutencaoSemPneus > 0 ? Math.Round(manutencaoSemPneus / km12, 4) : null,
            GastoManutencao12Meses = resumos.Sum(r => r.Gasto12Meses),
        });
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

    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    public static string DescreverProxima(Shared.Manutencoes.ProximaManutencaoDto p)
    {
        var partes = new List<string>();
        if (p.ProximaKm is { } km)
            partes.Add(p.FaltamKm is { } fk && fk <= 0
                ? string.Create(PtBr, $"era aos {km:N0} km (passou {-fk:N0} km)")
                : string.Create(PtBr, $"aos {km:N0} km (faltam {p.FaltamKm:N0} km)"));
        if (p.ProximaData is { } data)
            partes.Add(p.FaltamDias is { } fd && fd <= 0
                ? $"era em {data:dd/MM/yyyy}"
                : $"em {data:dd/MM/yyyy} (faltam {p.FaltamDias} dias)");
        return $"{p.Tipo} do {p.Veiculo}: {string.Join(" ou ", partes)}.";
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

    private async Task<(RelatorioDto Relatorio, List<UsageRecord> SaidasDoPeriodo, ParametrosCusto Parametros, IReadOnlySet<Guid> DiaZero)> MontarAsync(
        DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var parametros = await parametrosRepository.ObterAsync(ct);
        var carregadas = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.CarregarDesde(de, ate), ct);
        var diaZero = await usageRepository.AbastecimentosDiaZeroAsync(ct);
        var relatorio = RelatorioCalculadora.Calcular(carregadas, de, ate, baseOperacional.Value, parametros, DateTimeOffset.UtcNow, diaZero);

        // Manutenção vencida / em breve entra nos alertas (vale o estado de hoje, não do período).
        var proximas = ManutencaoCalculo.Proximas(await manutencaoRepository.ListarAsync(null, ct),
            await vehicleRepository.ListAsync(ct), RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow));
        var alertasManutencao = proximas
            .Where(p => p.Situacao != Shared.Manutencoes.SituacaoManutencao.EmDia)
            .Select(p => new AlertaDto(
                p.Situacao == Shared.Manutencoes.SituacaoManutencao.Vencida ? "Manutenção vencida" : "Manutenção em breve",
                p.Situacao == Shared.Manutencoes.SituacaoManutencao.Vencida ? GravidadeAlerta.Alta : GravidadeAlerta.Media,
                DescreverProxima(p), null, null));
        relatorio = relatorio with { Alertas = [.. alertasManutencao, .. relatorio.Alertas] };
        var doPeriodo = carregadas.Where(u => RelatorioCalculadora.NoPeriodo(u, de, ate)).ToList();
        return (relatorio, doPeriodo, parametros, diaZero);
    }
}
