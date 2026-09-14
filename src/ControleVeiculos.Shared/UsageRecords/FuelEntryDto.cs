namespace ControleVeiculos.Shared.UsageRecords;

public record FuelEntryDto(Guid Id, decimal Litros, decimal ValorTotal, int Odometro, DateTimeOffset CreatedAt);
