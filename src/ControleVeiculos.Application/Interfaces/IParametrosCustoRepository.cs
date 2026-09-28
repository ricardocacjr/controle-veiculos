using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IParametrosCustoRepository
{
    /// <summary>Os parâmetros gravados, ou os padrões (sem gravar) se ainda não houver.</summary>
    Task<ParametrosCusto> ObterAsync(CancellationToken ct = default);

    Task SalvarAsync(ParametrosCusto parametros, CancellationToken ct = default);
}
