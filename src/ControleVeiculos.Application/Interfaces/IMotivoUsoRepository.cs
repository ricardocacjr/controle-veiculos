using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IMotivoUsoRepository : IRepository<MotivoUso>
{
    Task<MotivoUso?> GetByNomeAsync(string nome, CancellationToken ct = default);

    /// <summary>Garante que um motivo com esse nome existe no catálogo, criando se for novo —
    /// é assim que a lista "cresce sozinha" a cada uso iniciado com uma finalidade inédita.</summary>
    Task EnsureExistsAsync(string nome, CancellationToken ct = default);
}
