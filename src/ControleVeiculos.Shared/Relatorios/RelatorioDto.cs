namespace ControleVeiculos.Shared.Relatorios;

/// <summary>Relatório gerencial de um período (datas no horário de Brasília, "Ate" inclusivo).</summary>
public record RelatorioDto(
    DateOnly De,
    DateOnly Ate,
    ResumoDto Resumo,
    CustosDto Custos,
    IReadOnlyList<GrupoDto> PorMotorista,
    IReadOnlyList<GrupoDto> PorEmpresa,
    IReadOnlyList<GrupoDto> PorMotivo,
    IReadOnlyList<ConsumoMesDto> Consumo,
    IReadOnlyList<CicloDto> Ciclos,
    IReadOnlyList<AlertaDto> Alertas);

/// <summary>Km e horas contam só saídas encerradas; saídas em andamento aparecem à parte.</summary>
public record ResumoDto(
    int Saidas,
    int EmAndamento,
    int KmRodados,
    double Horas,
    int Abastecimentos,
    decimal Litros,
    decimal GastoCombustivel,
    decimal GastoEmpresa,
    decimal PagoPelosMotoristas,
    decimal DiaZeroForaDaConta = 0);

/// <summary>
/// Quanto custou o km e quanto cobrar. Combustível por km vem dos ciclos de tanque cheio → tanque
/// cheio (o abastecimento que fecha o ciclo paga os km rodados nele); extra = pneus + manutenção
/// (parâmetros). Valor a cobrar = km × tarifa; saída em que o motorista abasteceu do bolso fica quitada (zero).
/// </summary>
public record CustosDto(
    decimal? CombustivelPorKm,
    string? OrigemCombustivel,
    decimal PneusPorKm,
    decimal ManutencaoPorKm,
    decimal ExtraPorKm,
    decimal? CustoTotalPorKm,
    decimal TarifaPorKm,
    decimal CustoReal,
    decimal ValorACobrar,
    decimal? KmPorLitro,
    decimal? PrecoMedioLitro,
    decimal? CombustivelDosKm = null);

public record GrupoDto(
    string Nome,
    int Saidas,
    int KmRodados,
    double Horas,
    decimal GastoCombustivel,
    double PercentualKm = 0,
    decimal CustoReal = 0,
    decimal PagoPeloMotorista = 0,
    decimal ValorACobrar = 0);

/// <summary>Consumo do mês, medido pelos ciclos de tanque cheio que fecharam no mês.</summary>
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

/// <summary>De um tanque cheio ao seguinte: km rodados ÷ litros postos = consumo real.</summary>
public record CicloDto(
    string Veiculo,
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    int KmInicial,
    int KmFinal,
    int Km,
    int Abastecimentos,
    decimal Litros,
    decimal Gasto,
    decimal? KmPorLitro,
    decimal? CustoPorKm);

public enum GravidadeAlerta { Baixa = 0, Media = 1, Alta = 2 }

public record AlertaDto(
    string Tipo,
    GravidadeAlerta Gravidade,
    string Descricao,
    Guid? UsoId,
    DateTimeOffset? Quando);

/// <summary>Parâmetros de custo editáveis pelo admin + o custo calculado recente, pra ajudar a revisar a tarifa.</summary>
public record ParametrosCustoDto(
    decimal TarifaPorKm,
    decimal PrecoJogoPneus,
    int VidaUtilPneusKm,
    decimal ManutencaoPorKm,
    decimal PneusPorKm = 0,
    decimal ExtraPorKm = 0,
    decimal? CombustivelPorKmRecente = null,
    decimal? CustoTotalPorKmRecente = null,
    decimal? KmPorLitroRecente = null);
