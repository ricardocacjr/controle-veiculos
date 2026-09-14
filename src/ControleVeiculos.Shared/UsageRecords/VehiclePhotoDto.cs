using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Shared.UsageRecords;

public record VehiclePhotoDto(Guid Id, VehiclePhotoType Tipo, string ArquivoUrl, string? Observacao, DateTimeOffset CreatedAt);
