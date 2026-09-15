namespace ControleVeiculos.Shared.Auth;

/// <summary>Login aceita e-mail (equipe) ou usuário curto (motoristas, ex: "DANI") — ver
/// <see cref="RegisterRequest.Login"/>.</summary>
public record LoginRequest(string Login, string Password);
