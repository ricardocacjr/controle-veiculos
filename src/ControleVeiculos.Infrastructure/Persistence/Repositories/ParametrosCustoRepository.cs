using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class ParametrosCustoRepository(AppDbContext context) : IParametrosCustoRepository
{
    public async Task<ParametrosCusto> ObterAsync(CancellationToken ct = default) =>
        await context.Set<ParametrosCusto>().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct) ?? new ParametrosCusto();

    public async Task SalvarAsync(ParametrosCusto parametros, CancellationToken ct = default)
    {
        var atual = await context.Set<ParametrosCusto>().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        if (atual is null)
        {
            await context.Set<ParametrosCusto>().AddAsync(parametros, ct);
        }
        else
        {
            atual.TarifaPorKm = parametros.TarifaPorKm;
            atual.PrecoJogoPneus = parametros.PrecoJogoPneus;
            atual.VidaUtilPneusKm = parametros.VidaUtilPneusKm;
            atual.ManutencaoPorKm = parametros.ManutencaoPorKm;
            atual.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(ct);
    }
}
