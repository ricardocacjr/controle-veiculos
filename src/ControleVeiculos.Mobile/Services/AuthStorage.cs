namespace ControleVeiculos.Mobile.Services;

/// <summary>Guarda o token JWT no armazenamento seguro do dispositivo entre sessões do app.</summary>
public class AuthStorage
{
    private const string TokenKey = "auth_token";

    public string? Token { get; private set; }

    public async Task LoadAsync()
    {
        Token = await SecureStorage.Default.GetAsync(TokenKey);
    }

    public async Task SetTokenAsync(string token)
    {
        Token = token;
        await SecureStorage.Default.SetAsync(TokenKey, token);
    }

    public void SignOut()
    {
        Token = null;
        SecureStorage.Default.Remove(TokenKey);
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
}
