namespace ControleVeiculos.Shared.Auth;

/// <param name="PrecisaDefinirPin">true = ainda com PIN temporário; o app leva direto pra tela de
/// criar o próprio PIN antes de qualquer outra coisa.</param>
public record AuthResponse(string Token, bool PrecisaDefinirPin = false);
