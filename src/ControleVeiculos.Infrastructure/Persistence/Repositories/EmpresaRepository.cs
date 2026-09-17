using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class EmpresaRepository(AppDbContext context) : RepositoryBase<Empresa>(context), IEmpresaRepository
{
    public async Task<Empresa?> GetByNomeAsync(string nome, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(e => e.Nome == nome, ct);
}
