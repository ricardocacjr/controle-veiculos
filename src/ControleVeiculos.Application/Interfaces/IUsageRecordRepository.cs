using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Application.Interfaces;

public interface IUsageRecordRepository : IRepository<UsageRecord>
{
    Task<UsageRecord?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UsageRecord>> ListByMotoristaAsync(Guid motoristaId, CancellationToken ct = default);
    Task<IReadOnlyList<UsageRecord>> ListByVeiculoAsync(Guid veiculoId, CancellationToken ct = default);
    Task<UsageRecord?> GetEmAndamentoByMotoristaAsync(Guid motoristaId, CancellationToken ct = default);

    /// <summary>Saídas iniciadas a partir de <paramref name="desde"/> + todas ainda em andamento, com abastecimentos e fotos (sem os arquivos).</summary>
    Task<IReadOnlyList<UsageRecord>> ListParaRelatorioAsync(DateTimeOffset desde, CancellationToken ct = default);

    /// <summary>Apaga as saídas (todas, se <paramref name="ids"/> for null; menos as importadas, se <paramref name="manterImportados"/>) com fotos, notas de voz e abastecimentos. Devolve os ids apagados.</summary>
    /// <summary>Motivos (texto exato) usados nas saídas, com a quantidade de saídas de cada um.</summary>
    Task<IReadOnlyList<(string Finalidade, int Saidas)>> ContarFinalidadesAsync(CancellationToken ct = default);

    /// <summary>Troca o motivo <paramref name="de"/> por <paramref name="para"/> em todas as saídas. Devolve quantas mudaram.</summary>
    Task<int> RenomearFinalidadeAsync(string de, string para, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> ExcluirAsync(IReadOnlyCollection<Guid>? ids, bool manterImportados = false, CancellationToken ct = default);
    Task AddPhotoAsync(VehiclePhoto photo, CancellationToken ct = default);
    Task AddVoiceNoteAsync(VoiceNote note, CancellationToken ct = default);
    Task AddFuelEntryAsync(FuelEntry entry, CancellationToken ct = default);
}
