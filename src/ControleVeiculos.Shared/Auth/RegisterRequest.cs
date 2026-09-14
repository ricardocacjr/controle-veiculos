namespace ControleVeiculos.Shared.Auth;

public record RegisterRequest(string NomeCompleto, string Email, string Password, string Role);
