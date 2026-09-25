namespace ControleVeiculos.Shared.UsageRecords;

public record MotivoUsoDto(Guid Id, string Nome);

public record CreateMotivoUsoRequest(string Nome);
