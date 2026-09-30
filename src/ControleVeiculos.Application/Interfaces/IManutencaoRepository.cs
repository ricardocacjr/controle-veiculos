using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IManutencaoRepository : IRepository<Manutencao>
{
    /// <summary>Manutenções (de um carro, ou de todos), mais recentes primeiro, com o veículo.</summary>
    Task<IReadOnlyList<Manutencao>> ListarAsync(Guid? veiculoId, CancellationToken ct = default);
}
