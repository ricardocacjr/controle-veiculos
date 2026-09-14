using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Vehicles;

namespace ControleVeiculos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class VehiclesController(IVehicleRepository vehicleRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> List(CancellationToken ct)
    {
        var vehicles = await vehicleRepository.ListAsync(ct);
        return Ok(vehicles.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> GetById(Guid id, CancellationToken ct)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(id, ct);
        return vehicle is null ? NotFound() : Ok(ToDto(vehicle));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<ActionResult<VehicleDto>> Create(CreateVehicleRequest request, CancellationToken ct)
    {
        if (await vehicleRepository.GetByPlacaAsync(request.Placa, ct) is not null)
            return BadRequest("Já existe um veículo com essa placa.");

        var vehicle = new Vehicle
        {
            Placa = request.Placa,
            Marca = request.Marca,
            Modelo = request.Modelo,
            Ano = request.Ano,
            Cor = request.Cor,
            OdometroAtual = request.OdometroAtual,
        };

        await vehicleRepository.AddAsync(vehicle, ct);
        await vehicleRepository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, ToDto(vehicle));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Gestor)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(id, ct);
        if (vehicle is null)
            return NotFound();

        vehicleRepository.Remove(vehicle);
        await vehicleRepository.SaveChangesAsync(ct);
        return NoContent();
    }

    private static VehicleDto ToDto(Vehicle v) => new(v.Id, v.Placa, v.Marca, v.Modelo, v.Ano, v.Cor, v.OdometroAtual, v.Status);
}
