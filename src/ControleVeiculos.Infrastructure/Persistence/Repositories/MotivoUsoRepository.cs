using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class MotivoUsoRepository(AppDbContext context) : RepositoryBase<MotivoUso>(context), IMotivoUsoRepository
{
    public async Task<MotivoUso?> GetByNomeAsync(string nome, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(m => m.Nome == nome, ct);

    public async Task EnsureExistsAsync(string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return;

        var existente = await GetByNomeAsync(nome, ct);
        if (existente is null)
            await Set.AddAsync(new MotivoUso { Nome = nome }, ct);
    }
}
