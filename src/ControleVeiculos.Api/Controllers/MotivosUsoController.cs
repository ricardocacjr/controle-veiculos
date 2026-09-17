using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.UsageRecords;

namespace ControleVeiculos.Api.Controllers;

/// <summary>
/// Catálogo de finalidades de uso — só leitura e limpeza aqui. Não tem POST porque a lista
/// cresce sozinha: toda vez que um uso é iniciado com uma finalidade nova (ver
/// UsageRecordsController.Start), ela entra automaticamente no catálogo.
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
