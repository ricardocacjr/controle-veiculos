namespace ControleVeiculos.Shared.Drivers;

public record CreateDriverRequest(string Nome, string Cnh, DateOnly? CnhValidade, string? Telefone, Guid? UserId);
