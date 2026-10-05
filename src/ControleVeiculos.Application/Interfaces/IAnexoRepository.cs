using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IAnexoRepository
{
    /// <summary>Guarda o arquivo (já salva no banco) e devolve o id.</summary>
    Task<Guid> SalvarAsync(string nome, string contentType, byte[] conteudo, CancellationToken ct = default);

    Task<Anexo?> ObterAsync(Guid id, CancellationToken ct = default);
}
