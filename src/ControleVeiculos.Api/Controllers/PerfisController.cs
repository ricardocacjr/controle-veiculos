using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ControleVeiculos.Api.Auth;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Shared.Auth;

namespace ControleVeiculos.Api.Controllers;

/// <summary>
/// Perfis dos motoristas (tela de escolha estilo Netflix + PIN). A lista e as fotos são
/// públicas de propósito: a tela de entrada precisa mostrá-las antes do login — expõe só nome e
/// foto, nunca e-mail/telefone.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PerfisController(UserManager<ApplicationUser> userManager, IDriverRepository driverRepository) : ControllerBase
{
    private const int TamanhoMaximoFoto = 400 * 1024;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PerfilDto>>> List()
    {
        var motoristas = await userManager.GetUsersInRoleAsync(Roles.Motorista);
        var admins = (await userManager.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();

        return Ok(motoristas
            .OrderBy(u => u.NomeCompleto)
            .Select(u => ToDto(u, admins.Contains(u.Id))));
    }

    [HttpGet("{id:guid}/foto")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFoto(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user?.Foto is null)
            return NotFound();

        // Cache só quando existe foto (a URL leva ?v=versão, então trocar a foto fura o cache).
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(user.Foto, user.FotoContentType ?? "image/jpeg");
    }

    /// <summary>O próprio usuário troca a foto dele; o Admin troca a de qualquer um.</summary>
    [HttpPut("{id:guid}/foto")]
    [Authorize]
    public async Task<IActionResult> SetFoto(Guid id, IFormFile file)
    {
        if (id != User.GetUserId() && !User.IsInRole(Roles.Admin))
            return Forbid();
        if (file.Length == 0 || file.Length > TamanhoMaximoFoto)
            return BadRequest("Foto vazia ou grande demais.");
        if (!file.ContentType.StartsWith("image/"))
            return BadRequest("O arquivo precisa ser uma imagem.");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        using var memoria = new MemoryStream();
        await file.CopyToAsync(memoria);
        user.Foto = memoria.ToArray();
        user.FotoContentType = file.ContentType;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    /// <summary>Cadastra um motorista já com login curto e PIN temporário (troca obrigatória no 1º acesso).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PerfilDto>> CriarMotorista(CriarMotoristaRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(request.Nome) || login.Length == 0)
            return BadRequest("Informe nome e usuário.");
        if (!AuthController.PinValido.IsMatch(request.PinTemporario))
            return BadRequest("O PIN temporário precisa ter exatamente 6 números.");
        if (await userManager.FindByNameAsync(login) is not null)
            return BadRequest($"Já existe um usuário \"{login}\".");

        var user = new ApplicationUser
        {
            UserName = login,
            Email = $"{login.ToLowerInvariant()}@controleveiculos.local",
            NomeCompleto = request.Nome.Trim(),
            PrecisaDefinirPin = true,
        };
        var result = await userManager.CreateAsync(user, request.PinTemporario);
        if (!result.Succeeded)
            return BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, Roles.Motorista);
        await driverRepository.AddAsync(new Driver { Nome = user.NomeCompleto, Cnh = string.Empty, UserId = user.Id }, ct);
        await driverRepository.SaveChangesAsync(ct);

        return Ok(ToDto(user, ehAdmin: false));
    }

    /// <summary>"Esqueci meu PIN": o Admin define um PIN temporário e o motorista cria outro no próximo acesso.</summary>
    [HttpPost("{id:guid}/redefinir-pin")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RedefinirPin(Guid id, RedefinirPinRequest request)
    {
        if (!AuthController.PinValido.IsMatch(request.PinTemporario))
            return BadRequest("O PIN temporário precisa ter exatamente 6 números.");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        // Remove + Add em vez de ResetPassword: AddIdentityCore não registra os token providers.
        await userManager.RemovePasswordAsync(user);
        var result = await userManager.AddPasswordAsync(user, request.PinTemporario);
        if (!result.Succeeded)
            return BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

        user.PrecisaDefinirPin = true;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return NoContent();
    }

    [HttpPut("{id:guid}/admin")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DefinirAdmin(Guid id, DefinirAdminRequest request)
    {
        if (id == User.GetUserId() && !request.Admin)
            return BadRequest("Você não pode tirar o seu próprio acesso de administrador.");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        var ehAdmin = await userManager.IsInRoleAsync(user, Roles.Admin);
        if (request.Admin && !ehAdmin)
            await userManager.AddToRoleAsync(user, Roles.Admin);
        else if (!request.Admin && ehAdmin)
            await userManager.RemoveFromRoleAsync(user, Roles.Admin);

        return NoContent();
    }

    private static PerfilDto ToDto(ApplicationUser u, bool ehAdmin) =>
        new(u.Id, u.UserName ?? "", u.NomeCompleto, u.Foto is not null, ehAdmin, u.PrecisaDefinirPin,
            u.Foto is null ? null : $"{u.Foto.Length:x}{u.ConcurrencyStamp?[..8]}");
}
