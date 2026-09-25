namespace ControleVeiculos.Shared.UsageRecords;

public record AddFuelEntryRequest(
    decimal Litros, decimal ValorTotal, int Odometro, decimal? ValorPorLitro = null,
    double? Latitude = null, double? Longitude = null);
