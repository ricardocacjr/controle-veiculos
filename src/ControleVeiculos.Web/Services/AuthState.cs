using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ControleVeiculos.Web.Services;

/// <summary>
/// Guarda o token JWT em memória durante o circuito do Blazor Server. Suficiente para
/// validar o fluxo ponta a ponta; sessão persistente entre recarregamentos de página
/// (cookie/localStorage) fica para quando o módulo de auth for aprofundado.
/// </summary>
public class AuthState
{
    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public Guid? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public event Action? Changed;

    public void SignIn(string token, string email)
    {
        Token = token;
        Email = email;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        UserId = ExtractUserId(jwt);
        Roles = jwt.Claims.Where(c => c.Type is ClaimTypes.Role or "role").Select(c => c.Value).ToList();
        Changed?.Invoke();
    }

    public void SignOut()
    {
        Token = null;
        Email = null;
        UserId = null;
        Roles = [];
        Changed?.Invoke();
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public bool IsInRole(string role) => Roles.Contains(role);

    private static Guid? ExtractUserId(JwtSecurityToken jwt)
    {
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
