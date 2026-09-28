namespace ControleVeiculos.Shared.UsageRecords;

/// <param name="SemFoto">Motorista sem câmera digitou o km — finaliza sem a foto do painel e vira alerta no relatório.</param>
public record FinishUsageRequest(int OdometroFinal, bool SemFoto = false);
