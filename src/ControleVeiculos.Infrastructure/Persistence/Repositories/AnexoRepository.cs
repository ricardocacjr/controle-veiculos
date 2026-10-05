using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Repositories;

public class AnexoRepository(AppDbContext context) : IAnexoRepository
{
    public async Task<Guid> SalvarAsync(string nome, string contentType, byte[] conteudo, CancellationToken ct = default)
    {
        var anexo = new Anexo { Nome = nome, ContentType = contentType, Conteudo = conteudo };
        await context.Set<Anexo>().AddAsync(anexo, ct);
        await context.SaveChangesAsync(ct);
        return anexo.Id;
    }

    public async Task<Anexo?> ObterAsync(Guid id, CancellationToken ct = default) =>
        await context.Set<Anexo>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
}
