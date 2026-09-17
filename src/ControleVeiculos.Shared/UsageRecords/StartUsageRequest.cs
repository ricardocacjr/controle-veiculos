namespace ControleVeiculos.Shared.UsageRecords;

public record StartUsageRequest(
    Guid VeiculoId,
    string Finalidade,
    string? Origem,
    string? Destino,
    int OdometroInicial,
    double? Latitude = null,
    double? Longitude = null);
