using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class VehicleRepository(AppDbContext context) : RepositoryBase<Vehicle>(context), IVehicleRepository
{
    public async Task<Vehicle?> GetByPlacaAsync(string placa, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(v => v.Placa == placa, ct);
}
