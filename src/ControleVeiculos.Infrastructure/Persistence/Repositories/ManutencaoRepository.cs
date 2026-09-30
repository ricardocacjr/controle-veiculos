using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class ManutencaoRepository(AppDbContext context) : RepositoryBase<Manutencao>(context), IManutencaoRepository
{
    public async Task<IReadOnlyList<Manutencao>> ListarAsync(Guid? veiculoId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(m => m.Veiculo)
            .Where(m => veiculoId == null || m.VeiculoId == veiculoId)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Km)
            .ToListAsync(ct);
}
