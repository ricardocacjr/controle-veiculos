using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class DriverRepository(AppDbContext context) : RepositoryBase<Driver>(context), IDriverRepository
{
    public async Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(d => d.UserId == userId, ct);
}
