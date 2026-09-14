namespace ControleVeiculos.Shared.Drivers;

public record DriverDto(Guid Id, string Nome, string Cnh, DateOnly? CnhValidade, string? Telefone);
