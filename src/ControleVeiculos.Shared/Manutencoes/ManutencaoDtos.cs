namespace ControleVeiculos.Shared.Manutencoes;

public record ManutencaoDto(
    Guid Id,
    Guid VeiculoId,
    string VeiculoPlaca,
    DateOnly Data,
    int Km,
    string Tipo,
    string? Descricao,
    decimal Valor,
    string? Oficina,
    int? ProximaKm,
    DateOnly? ProximaData);

public record SalvarManutencaoRequest(
    Guid VeiculoId,
    DateOnly Data,
    int Km,
    string Tipo,
    string? Descricao,
    decimal Valor,
    string? Oficina,
    int? ProximaKm,
    DateOnly? ProximaData);

public enum SituacaoManutencao { EmDia = 0, EmBreve = 1, Vencida = 2 }

/// <summary>A última manutenção de cada tipo que tem "próxima" marcada, com quanto falta.</summary>
public record ProximaManutencaoDto(
    Guid VeiculoId,
    string Veiculo,
    string Tipo,
    int? ProximaKm,
    DateOnly? ProximaData,
    int? FaltamKm,
    int? FaltamDias,
    SituacaoManutencao Situacao,
    Guid UltimaManutencaoId);

/// <summary>Resumo de um carro: km atual, gasto e custo por km dos últimos 12 meses, próximas manutenções.</summary>
public record ManutencaoResumoDto(
    Guid VeiculoId,
    string Veiculo,
    int OdometroAtual,
    decimal Gasto12Meses,
    int KmRodados12Meses,
    decimal? CustoPorKmSemPneus,
    IReadOnlyList<ProximaManutencaoDto> Proximas);
