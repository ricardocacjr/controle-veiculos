using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.UsageRecords;

namespace ControleVeiculos.Api.Controllers;

/// <summary>
/// Catálogo de finalidades de uso. Cresce sozinho — toda finalidade nova digitada numa saída
/// entra no catálogo (ver UsageRecordsController.Start) — mas o Admin também pode deixar
/// motivos prontos, pra aparecerem como botão já no primeiro uso, e padronizar os que já
/// foram usados (renomear ou juntar dois motivos num só em todas as saídas).
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MotivosUsoController(IMotivoUsoRepository motivoUsoRepository, IUsageRecordRepository usageRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MotivoUsoDto>>> List(CancellationToken ct)
    {
        var motivos = await motivoUsoRepository.ListAsync(ct);
        return Ok(motivos.Select(m => new MotivoUsoDto(m.Id, m.Nome)));
    }

    /// <summary>Todos os motivos: os usados nas saídas (com quantidade) + os só do catálogo.</summary>
    [HttpGet("em-uso")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<ActionResult<IEnumerable<MotivoEmUsoDto>>> EmUso(CancellationToken ct)
    {
        var usados = await usageRepository.ContarFinalidadesAsync(ct);
        var catalogo = (await motivoUsoRepository.ListAsync(ct)).Select(m => m.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lista = usados
            .Select(u => new MotivoEmUsoDto(u.Finalidade, u.Saidas, catalogo.Contains(u.Finalidade)))
            .Concat(catalogo
                .Where(nome => !usados.Any(u => string.Equals(u.Finalidade, nome, StringComparison.OrdinalIgnoreCase)))
                .Select(nome => new MotivoEmUsoDto(nome, 0, true)))
            .OrderBy(m => m.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), ignoreCase: true))
            .ToList();
        return Ok(lista);
    }

    /// <summary>Troca um motivo por outro em todas as saídas (e no catálogo). Renomear ou juntar.</summary>
    [HttpPost("renomear")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RenomearMotivoResponse>> Renomear(RenomearMotivoRequest request, CancellationToken ct)
    {
        var de = request.De ?? "";
        var para = request.Para?.Trim() ?? "";
        if (de.Length == 0 || para.Length == 0)
            return BadRequest("Informe o motivo atual e o novo nome.");
        if (de == para)
            return Ok(new RenomearMotivoResponse(0));

        var saidas = await usageRepository.RenomearFinalidadeAsync(de, para, ct);

        // Catálogo: o nome antigo sai (senão continuaria aparecendo como botão pro motorista) e o novo entra.
        foreach (var antigo in (await motivoUsoRepository.ListAsync(ct)).Where(m => m.Nome == de))
            motivoUsoRepository.Remove(antigo);
        await motivoUsoRepository.EnsureExistsAsync(para, ct);
        await motivoUsoRepository.SaveChangesAsync(ct);

        return Ok(new RenomearMotivoResponse(saidas));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<IActionResult> Create(CreateMotivoUsoRequest request, CancellationToken ct)
    {
        var nome = request.Nome.Trim();
        if (nome.Length == 0)
            return BadRequest("Informe o motivo.");

        await motivoUsoRepository.EnsureExistsAsync(nome, ct);
        await motivoUsoRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var motivo = await motivoUsoRepository.GetByIdAsync(id, ct);
        if (motivo is null)
            return NotFound();

        motivoUsoRepository.Remove(motivo);
        await motivoUsoRepository.SaveChangesAsync(ct);
        return NoContent();
    }
}
