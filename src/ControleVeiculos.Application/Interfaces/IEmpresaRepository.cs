using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IEmpresaRepository : IRepository<Empresa>
{
    Task<Empresa?> GetByNomeAsync(string nome, CancellationToken ct = default);
}
