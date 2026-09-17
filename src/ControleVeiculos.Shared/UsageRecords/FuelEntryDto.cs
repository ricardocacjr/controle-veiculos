namespace ControleVeiculos.Shared.UsageRecords;

public record FuelEntryDto(Guid Id, decimal Litros, decimal ValorTotal, decimal? ValorPorLitro, int Odometro, DateTimeOffset CreatedAt);
