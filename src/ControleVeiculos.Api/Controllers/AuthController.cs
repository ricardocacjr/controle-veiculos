using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Api.Auth;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Auth;

namespace ControleVeiculos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Cadastro de conta (Admin, Gestor ou Motorista). Sem restrição de papel por enquanto —
    /// ver "Limitações conhecidas" no README: qualquer um pode se cadastrar como Admin.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!Roles.All.Contains(request.Role))
            return BadRequest($"Papel inválido. Use um de: {string.Join(", ", Roles.All)}");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            NomeCompleto = request.NomeCompleto,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await userManager.AddToRoleAsync(user, request.Role);

        var token = tokenService.GenerateToken(user, [request.Role]);
        return Ok(new AuthResponse(token));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized();

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.GenerateToken(user, roles);
        return Ok(new AuthResponse(token));
    }
}
