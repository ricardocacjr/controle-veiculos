using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Drivers;

namespace ControleVeiculos.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
[Route("api/[controller]")]
public class DriversController(IDriverRepository driverRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DriverDto>>> List(CancellationToken ct)
    {
        var drivers = await driverRepository.ListAsync(ct);
        return Ok(drivers.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DriverDto>> GetById(Guid id, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(id, ct);
        return driver is null ? NotFound() : Ok(ToDto(driver));
    }

    [HttpPost]
    public async Task<ActionResult<DriverDto>> Create(CreateDriverRequest request, CancellationToken ct)
    {
        var driver = new Driver
        {
            Nome = request.Nome,
            Cnh = request.Cnh,
            CnhValidade = request.CnhValidade,
            Telefone = request.Telefone,
            UserId = request.UserId,
        };

        await driverRepository.AddAsync(driver, ct);
        await driverRepository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, ToDto(driver));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(id, ct);
        if (driver is null)
            return NotFound();

        driverRepository.Remove(driver);
        await driverRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    private static DriverDto ToDto(Driver d) => new(d.Id, d.Nome, d.Cnh, d.CnhValidade, d.Telefone);
}
