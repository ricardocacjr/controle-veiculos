using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ControleVeiculos.Api.Relatorios;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Relatorios;

namespace ControleVeiculos.Api.Controllers;

/// <summary>Relatórios gerenciais (só administração). Período em datas de Brasília; sem período = mês atual.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
[Route("api/[controller]")]
public class RelatoriosController(IUsageRecordRepository usageRepository, IOptions<BaseOperacional> baseOperacional) : ControllerBase
{
    private const int DiasMaximos = 366 * 2;

    [HttpGet]
    public async Task<ActionResult<RelatorioDto>> Get([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        if (Periodo(de, ate) is not { } periodo)
            return BadRequest($"Período inválido (a data inicial deve ser antes da final, no máximo {DiasMaximos} dias).");

        var (relatorio, _) = await MontarAsync(periodo.De, periodo.Ate, ct);
        return Ok(relatorio);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> Excel([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        if (Periodo(de, ate) is not { } periodo)
            return BadRequest($"Período inválido (a data inicial deve ser antes da final, no máximo {DiasMaximos} dias).");

        var (relatorio, saidas) = await MontarAsync(periodo.De, periodo.Ate, ct);
        var arquivo = RelatorioExcel.Gerar(relatorio, saidas);
        return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"relatorio-veiculos-{periodo.De:yyyy-MM-dd}-a-{periodo.Ate:yyyy-MM-dd}.xlsx");
    }

    private static (DateOnly De, DateOnly Ate)? Periodo(DateOnly? de, DateOnly? ate)
    {
        var hoje = RelatorioCalculadora.Hoje(DateTimeOffset.UtcNow);
        var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = ate ?? hoje;
        if (fim < inicio || fim.DayNumber - inicio.DayNumber > DiasMaximos)
            return null;
        return (inicio, fim);
    }

    private async Task<(RelatorioDto Relatorio, List<Domain.Entities.UsageRecord> SaidasDoPeriodo)> MontarAsync(DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var carregadas = await usageRepository.ListParaRelatorioAsync(RelatorioCalculadora.CarregarDesde(de, ate), ct);
        var relatorio = RelatorioCalculadora.Calcular(carregadas, de, ate, baseOperacional.Value, DateTimeOffset.UtcNow);
        var doPeriodo = carregadas.Where(u => RelatorioCalculadora.NoPeriodo(u, de, ate)).ToList();
        return (relatorio, doPeriodo);
    }
}
