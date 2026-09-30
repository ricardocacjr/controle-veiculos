using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IManutencaoRepository : IRepository<Manutencao>
{
    /// <summary>Manutenções (de um carro, ou de todos), mais recentes primeiro, com veículo, itens e próximas.</summary>
    Task<IReadOnlyList<Manutencao>> ListarAsync(Guid? veiculoId, CancellationToken ct = default);

    /// <summary>Uma manutenção com itens e próximas, pronta pra editar.</summary>
    Task<Manutencao?> ObterParaEditarAsync(Guid id, CancellationToken ct = default);

    Task AdicionarAnexoAsync(ManutencaoAnexo anexo, CancellationToken ct = default);
    Task<ManutencaoAnexo?> ObterAnexoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Troca os itens e as próximas de uma manutenção pelos novos (edição).</summary>
    void SubstituirDetalhes(Manutencao manutencao, IEnumerable<ManutencaoItem> itens, IEnumerable<ManutencaoProxima> proximas);
}
