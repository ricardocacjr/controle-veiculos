using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class ManutencaoRepository(AppDbContext context) : RepositoryBase<Manutencao>(context), IManutencaoRepository
{
    public async Task<IReadOnlyList<Manutencao>> ListarAsync(Guid? veiculoId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(m => m.Veiculo)
            .Include(m => m.Itens)
            .Include(m => m.Proximas)
            .AsSplitQuery()
            .Where(m => veiculoId == null || m.VeiculoId == veiculoId)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Km)
            .ToListAsync(ct);

    public async Task<Manutencao?> ObterParaEditarAsync(Guid id, CancellationToken ct = default) =>
        await Set.Include(m => m.Itens).Include(m => m.Proximas).Include(m => m.Veiculo)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id, ct);


    public void SubstituirDetalhes(Manutencao manutencao, IEnumerable<ManutencaoItem> itens, IEnumerable<ManutencaoProxima> proximas)
    {
        Context.Set<ManutencaoItem>().RemoveRange(manutencao.Itens);
        Context.Set<ManutencaoProxima>().RemoveRange(manutencao.Proximas);
        manutencao.Itens = itens.ToList();
        manutencao.Proximas = proximas.ToList();
        Context.Set<ManutencaoItem>().AddRange(manutencao.Itens);
        Context.Set<ManutencaoProxima>().AddRange(manutencao.Proximas);
    }
}
