namespace ControleVeiculos.Shared.UsageRecords;

public record AddFuelEntryRequest(
    // ValorTotal = valor na bomba; o servidor grava o valor pago (ValorTotal − Desconto).
    decimal Litros, decimal ValorTotal, int Odometro, decimal? ValorPorLitro = null,
    double? Latitude = null, double? Longitude = null,
    bool TanqueCheio = true, bool PagoPeloMotorista = false, Guid? ComprovanteId = null, decimal Desconto = 0);
