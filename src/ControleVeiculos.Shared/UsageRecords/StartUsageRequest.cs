namespace ControleVeiculos.Shared.UsageRecords;

public record StartUsageRequest(
    Guid VeiculoId,
    string Finalidade,
    string? Origem,
    string? Destino,
    int OdometroInicial,
    Guid? EmpresaId = null,
    double? Latitude = null,
    double? Longitude = null);
