using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ControleVeiculos.Api.Auth;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Shared.Manutencoes;

namespace ControleVeiculos.Api.Controllers;

/// <summary>
/// Abre anexos (nota da oficina, cupom do posto) no navegador. O app pede um link assinado
/// (HMAC com a chave do JWT, vale 10 min) e o navegador abre direto no visualizador de PDF/foto,
/// sem baixar e sem precisar do token de login na URL.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnexosController(IAnexoRepository anexoRepository, IOptions<JwtOptions> jwt) : ControllerBase
{
    private static readonly TimeSpan ValidadeLink = TimeSpan.FromMinutes(10);

    [HttpGet("{id:guid}/link")]
    public async Task<ActionResult<LinkAnexoDto>> Link(Guid id, CancellationToken ct)
    {
        if (await anexoRepository.ObterAsync(id, ct) is null)
            return NotFound();
        var expira = DateTimeOffset.UtcNow.Add(ValidadeLink).ToUnixTimeSeconds();
        var url = Url.ActionLink(nameof(Abrir), values: new { id, exp = expira, sig = Assinar(id, expira) })!;
        return Ok(new LinkAnexoDto(url));
    }

    [HttpGet("{id:guid}/abrir")]
    [AllowAnonymous]
    public async Task<IActionResult> Abrir(Guid id, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken ct)
    {
        var esperado = Assinar(id, exp);
        if (sig is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(sig), Encoding.ASCII.GetBytes(esperado))
            || DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow)
            return StatusCode(StatusCodes.Status403Forbidden, "Link vencido. Volte ao app e toque em \"Ver\" de novo.");

        if (await anexoRepository.ObterAsync(id, ct) is not { } anexo)
            return NotFound();

        // "inline" = o navegador mostra (visualizador de PDF / imagem) em vez de baixar.
        Response.Headers.ContentDisposition = $"inline; filename=\"{anexo.Nome}\"";
        return File(anexo.Conteudo, anexo.ContentType);
    }

    private string Assinar(Guid id, long expira)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(jwt.Value.Key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"anexo:{id:N}:{expira}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
