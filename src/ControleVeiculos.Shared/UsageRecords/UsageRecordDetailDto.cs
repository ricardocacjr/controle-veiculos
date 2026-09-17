using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Shared.UsageRecords;

public record UsageRecordDetailDto(
    Guid Id,
    Guid VeiculoId,
    string VeiculoPlaca,
    Guid MotoristaId,
    string MotoristaNome,
    string? EmpresaNome,
    string Finalidade,
    string? Origem,
    string? Destino,
    int OdometroInicial,
    int? OdometroFinal,
    DateTimeOffset IniciadoEm,
    DateTimeOffset? FinalizadoEm,
    UsageRecordStatus Status,
    IReadOnlyList<VehiclePhotoDto> Fotos,
    IReadOnlyList<VoiceNoteDto> NotasDeVoz,
    IReadOnlyList<FuelEntryDto> Abastecimentos);
