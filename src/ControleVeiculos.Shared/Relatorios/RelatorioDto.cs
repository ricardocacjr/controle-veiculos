namespace ControleVeiculos.Shared.Relatorios;

/// <summary>Relatório gerencial de um período (datas no horário de Brasília, "Ate" inclusivo).</summary>
public record RelatorioDto(
    DateOnly De,
    DateOnly Ate,
    ResumoDto Resumo,
    IReadOnlyList<GrupoDto> PorMotorista,
    IReadOnlyList<GrupoDto> PorEmpresa,
    IReadOnlyList<GrupoDto> PorMotivo,
    IReadOnlyList<ConsumoMesDto> Consumo,
    IReadOnlyList<AlertaDto> Alertas);

/// <summary>Km e horas contam só saídas encerradas; saídas em andamento aparecem à parte.</summary>
public record ResumoDto(
    int Saidas,
    int EmAndamento,
    int KmRodados,
    double Horas,
    int Abastecimentos,
    decimal Litros,
    decimal GastoCombustivel);

public record GrupoDto(string Nome, int Saidas, int KmRodados, double Horas, decimal GastoCombustivel);

/// <summary>
/// Consumo médio do mês: km rodados no mês ÷ litros abastecidos no mês. É uma média — num mês
/// isolado pode oscilar (abasteceu no fim do mês e rodou no seguinte); a tendência vale mais.
/// </summary>
public record ConsumoMesDto(
    int Ano,
    int Mes,
    string Veiculo,
    int KmRodados,
    decimal Litros,
    decimal Gasto,
    decimal? KmPorLitro,
    decimal? CustoPorKm,
    decimal? PrecoMedioLitro);

public enum GravidadeAlerta { Baixa = 0, Media = 1, Alta = 2 }

public record AlertaDto(
    string Tipo,
    GravidadeAlerta Gravidade,
    string Descricao,
    Guid? UsoId,
    DateTimeOffset? Quando);
