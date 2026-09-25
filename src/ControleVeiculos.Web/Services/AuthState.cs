using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.JSInterop;

namespace ControleVeiculos.Web.Services;

/// <summary>
/// Sessão do usuário no circuito do Blazor, espelhada no localStorage do aparelho — assim o
/// motorista que abre o app pela tela de início já entra direto no "uso", sem PIN toda vez,
/// até o token expirar. O PIN nunca é guardado; só o token JWT (assinado e com validade).
/// </summary>
public class AuthState(IJSRuntime js)
{
    public string? Token { get; private set; }
    public string? Login { get; private set; }
    public string? Nome { get; private set; }
    public Guid? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public bool PrecisaDefinirPin { get; private set; }

    /// <summary>Só em memória, só durante o primeiro acesso — necessário pra trocar pelo PIN definitivo.</summary>
    public string? PinTemporario { get; private set; }

    /// <summary>false até tentar ler a sessão salva no aparelho (o layout espera isso antes de mostrar as telas).</summary>
    public bool Restaurado { get; private set; }

    /// <summary>Muda quando o usuário troca a própria foto — a foto do topo recarrega na hora.</summary>
    public string? FotoVersao { get; private set; }

    public event Action? Changed;

    public void FotoAlterada()
    {
        FotoVersao = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        Changed?.Invoke();
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool EhMotorista => IsInRole("Motorista");
    public bool EhGestao => IsInRole("Admin") || IsInRole("Gestor");
    public string PrimeiroNome => (Nome ?? Login ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    public async Task EntrarAsync(string token, string login, bool precisaDefinirPin, string? pinTemporario = null)
    {
        Aplicar(token, login, precisaDefinirPin);
        PinTemporario = precisaDefinirPin ? pinTemporario : null;
        await SalvarAsync();
        Changed?.Invoke();
    }

    public async Task PinDefinidoAsync(string novoToken)
    {
        Aplicar(novoToken, Login ?? "", precisaDefinirPin: false);
        PinTemporario = null;
        await SalvarAsync();
        Changed?.Invoke();
    }

    public async Task SairAsync()
    {
        Token = Login = Nome = PinTemporario = null;
        UserId = null;
        Roles = [];
        PrecisaDefinirPin = false;
        try { await js.InvokeVoidAsync("cvSessao.limpar"); } catch (JSDisconnectedException) { }
        Changed?.Invoke();
    }

    public async Task RestaurarAsync()
    {
        try
        {
            var salva = await js.InvokeAsync<SessaoSalva?>("cvSessao.ler");
            if (salva?.Token is { Length: > 0 } token && TokenValido(token))
                Aplicar(token, salva.Login ?? "", salva.PrecisaDefinirPin);
            else if (salva is not null)
                await js.InvokeVoidAsync("cvSessao.limpar");
        }
        catch (JSException)
        {
            // localStorage indisponível (modo privado etc.) — segue sem sessão salva.
        }

        Restaurado = true;
        Changed?.Invoke();
    }

    private void Aplicar(string token, string login, bool precisaDefinirPin)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Token = token;
        Login = login;
        PrecisaDefinirPin = precisaDefinirPin;
        Nome = jwt.Claims.FirstOrDefault(c => c.Type is ClaimTypes.Name or "unique_name" or "name")?.Value;
        UserId = Guid.TryParse(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
        Roles = jwt.Claims.Where(c => c.Type is ClaimTypes.Role or "role").Select(c => c.Value).ToList();
    }

    private async Task SalvarAsync()
    {
        try
        {
            await js.InvokeVoidAsync("cvSessao.salvar", new SessaoSalva(Token, Login, PrecisaDefinirPin));
        }
        catch (JSException) { }
    }

    private static bool TokenValido(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo > DateTime.UtcNow.AddMinutes(5);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public record SessaoSalva(string? Token, string? Login, bool PrecisaDefinirPin);
}
