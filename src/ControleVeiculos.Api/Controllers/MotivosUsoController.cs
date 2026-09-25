using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.UsageRecords;

namespace ControleVeiculos.Api.Controllers;

/// <summary>
/// Catálogo de finalidades de uso. Cresce sozinho — toda finalidade nova digitada numa saída
/// entra no catálogo (ver UsageRecordsController.Start) — mas o Admin também pode deixar
/// motivos prontos, pra aparecerem como botão já no primeiro uso.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MotivosUsoController(IMotivoUsoRepository motivoUsoRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MotivoUsoDto>>> List(CancellationToken ct)
    {
        var motivos = await motivoUsoRepository.ListAsync(ct);
        return Ok(motivos.Select(m => new MotivoUsoDto(m.Id, m.Nome)));
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
