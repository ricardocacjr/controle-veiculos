namespace ControleVeiculos.Shared.Auth;

/// <summary>Um perfil na tela de escolha (foto + nome), estilo Netflix.</summary>
/// <param name="FotoVersao">Muda quando a foto muda — vai na URL da foto pra furar o cache.</param>
public record PerfilDto(Guid UserId, string Login, string Nome, bool TemFoto, bool EhAdmin, bool PrecisaDefinirPin, string? FotoVersao = null);

public record DefinirPinRequest(string PinAtual, string NovoPin);

public record RedefinirPinRequest(string PinTemporario);

public record CriarMotoristaRequest(string Nome, string Login, string PinTemporario);

public record DefinirAdminRequest(bool Admin);
