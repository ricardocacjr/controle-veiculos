namespace ControleVeiculos.Shared.UsageRecords;

/// <summary>Correção de uma saída pelo admin (padronizar motivo, trocar empresa, acertar km/horário...).</summary>
public record UpdateUsageRequest(
    Guid MotoristaId,
    Guid? EmpresaId,
    string Finalidade,
    int OdometroInicial,
    int? OdometroFinal,
    DateTimeOffset IniciadoEm,
    DateTimeOffset? FinalizadoEm,
    string? Observacao,
    string? Origem = null,
    string? Destino = null);

public record UpdateFuelEntryRequest(decimal Litros, decimal ValorTotal, int Odometro, bool TanqueCheio, bool PagoPeloMotorista);

/// <summary>Motivo como aparece nas saídas (texto exato) e quantas saídas o usam.</summary>
public record MotivoEmUsoDto(string Nome, int Saidas, bool NoCatalogo);

/// <summary>Troca um motivo por outro em todas as saídas (renomear ou juntar dois motivos num só).</summary>
public record RenomearMotivoRequest(string De, string Para);

public record RenomearMotivoResponse(int Saidas);
