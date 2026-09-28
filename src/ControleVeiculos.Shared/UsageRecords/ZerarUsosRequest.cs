namespace ControleVeiculos.Shared.UsageRecords;

/// <summary>Confirmação digitada ("ZERAR") pra apagar as saídas; por padrão mantém o histórico importado da planilha.</summary>
public record ZerarUsosRequest(string? Confirmacao, bool ManterImportados = true);

public record ZerarUsosResponse(int Apagadas);
