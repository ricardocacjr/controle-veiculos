using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ControleVeiculos.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token não contém o claim de usuário (sub).");

        return Guid.Parse(value);
    }
}
