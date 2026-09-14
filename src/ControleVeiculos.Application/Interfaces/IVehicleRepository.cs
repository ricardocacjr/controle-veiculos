using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<Vehicle?> GetByPlacaAsync(string placa, CancellationToken ct = default);
}
