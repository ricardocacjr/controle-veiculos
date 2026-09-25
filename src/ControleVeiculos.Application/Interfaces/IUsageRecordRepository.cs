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

    /// <summary>Apaga as saídas (todas, se <paramref name="ids"/> for null) com fotos, notas de voz e abastecimentos. Devolve os ids apagados.</summary>
    Task<IReadOnlyList<Guid>> ExcluirAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct = default);
    Task AddPhotoAsync(VehiclePhoto photo, CancellationToken ct = default);
    Task AddVoiceNoteAsync(VoiceNote note, CancellationToken ct = default);
    Task AddFuelEntryAsync(FuelEntry entry, CancellationToken ct = default);
}
