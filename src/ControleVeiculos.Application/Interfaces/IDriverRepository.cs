using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IDriverRepository : IRepository<Driver>
{
    Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
