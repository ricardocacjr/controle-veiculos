using Microsoft.AspNetCore.Identity;

namespace ControleVeiculos.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string NomeCompleto { get; set; }
    public Guid? TenantId { get; set; }

    /// <summary>
    /// true enquanto o usuário ainda está com o PIN temporário dado pelo Admin — no próximo
    /// login o app obriga a criar o próprio PIN antes de liberar qualquer tela.
    /// </summary>
    public bool PrecisaDefinirPin { get; set; }

    /// <summary>Foto do perfil (tela de escolha estilo Netflix). Já chega reduzida pelo navegador
    /// (~256px JPEG), por isso cabe direto no banco — o disco do container é efêmero.</summary>
    public byte[]? Foto { get; set; }
    public string? FotoContentType { get; set; }
}
