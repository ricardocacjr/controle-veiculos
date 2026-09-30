namespace ControleVeiculos.Shared.UsageRecords;

/// <param name="SemFoto">Motorista sem câmera digitou o km — finaliza sem a foto do painel e vira alerta no relatório.</param>
/// <param name="LocalChegada">Onde o carro ficou ("Base - ...", "OFICINA", endereço do GPS...).</param>
public record FinishUsageRequest(int OdometroFinal, bool SemFoto = false, string? LocalChegada = null, double? Latitude = null, double? Longitude = null);
