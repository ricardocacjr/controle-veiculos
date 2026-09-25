namespace ControleVeiculos.Shared.UsageRecords;

/// <summary>Confirmação digitada ("ZERAR") pra apagar todas as saídas.</summary>
public record ZerarUsosRequest(string? Confirmacao);

public record ZerarUsosResponse(int Apagadas);
