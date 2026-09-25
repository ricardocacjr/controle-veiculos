using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
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
    internal static readonly Regex PinValido = new(@"^\d{6}$", RegexOptions.Compiled);

    /// <summary>Cadastro de conta — só Admin (antes era aberto, qualquer um virava Admin).</summary>
    [HttpPost("register")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!Roles.All.Contains(request.Role))
            return BadRequest($"Papel inválido. Use um de: {string.Join(", ", Roles.All)}");

        var user = new ApplicationUser
        {
            UserName = request.Login ?? request.Email,
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
        var user = await userManager.FindByNameAsync(request.Login) ?? await userManager.FindByEmailAsync(request.Login);
        if (user is null)
            return Unauthorized();

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, "Muitas tentativas erradas. Aguarde 5 minutos e tente de novo.");
        if (!result.Succeeded)
            return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.GenerateToken(user, roles);
        return Ok(new AuthResponse(token, user.PrecisaDefinirPin));
    }

    /// <summary>Primeiro acesso (ou troca): troca o PIN temporário pelo PIN escolhido pelo usuário.</summary>
    [HttpPost("definir-pin")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> DefinirPin(DefinirPinRequest request)
    {
        if (!PinValido.IsMatch(request.NovoPin))
            return BadRequest("O PIN precisa ter exatamente 6 números.");
        if (request.NovoPin == request.PinAtual)
            return BadRequest("O novo PIN precisa ser diferente do PIN temporário.");

        var user = await userManager.FindByIdAsync(User.GetUserId().ToString());
        if (user is null)
            return Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.PinAtual, request.NovoPin);
        if (!result.Succeeded)
            return BadRequest("PIN atual incorreto.");

        user.PrecisaDefinirPin = false;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new AuthResponse(tokenService.GenerateToken(user, roles)));
    }
}
