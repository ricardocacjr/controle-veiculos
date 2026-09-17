using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Empresas;

namespace ControleVeiculos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EmpresasController(IEmpresaRepository empresaRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmpresaDto>>> List(CancellationToken ct)
    {
        var empresas = await empresaRepository.ListAsync(ct);
        return Ok(empresas.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmpresaDto>> GetById(Guid id, CancellationToken ct)
    {
        var empresa = await empresaRepository.GetByIdAsync(id, ct);
        return empresa is null ? NotFound() : Ok(ToDto(empresa));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<ActionResult<EmpresaDto>> Create(CreateEmpresaRequest request, CancellationToken ct)
    {
        if (await empresaRepository.GetByNomeAsync(request.Nome, ct) is not null)
            return BadRequest("Já existe uma empresa com esse nome.");

        var empresa = new Empresa { Nome = request.Nome };

        await empresaRepository.AddAsync(empresa, ct);
        await empresaRepository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = empresa.Id }, ToDto(empresa));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<ActionResult<EmpresaDto>> Update(Guid id, UpdateEmpresaRequest request, CancellationToken ct)
    {
        var empresa = await empresaRepository.GetByIdAsync(id, ct);
        if (empresa is null)
            return NotFound();

        var empresaComMesmoNome = await empresaRepository.GetByNomeAsync(request.Nome, ct);
        if (empresaComMesmoNome is not null && empresaComMesmoNome.Id != id)
            return BadRequest("Já existe uma empresa com esse nome.");

        empresa.Nome = request.Nome;
        empresa.UpdatedAt = DateTimeOffset.UtcNow;

        empresaRepository.Update(empresa);
        await empresaRepository.SaveChangesAsync(ct);

        return Ok(ToDto(empresa));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var empresa = await empresaRepository.GetByIdAsync(id, ct);
        if (empresa is null)
            return NotFound();

        empresaRepository.Remove(empresa);
        await empresaRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    private static EmpresaDto ToDto(Empresa e) => new(e.Id, e.Nome);
}
