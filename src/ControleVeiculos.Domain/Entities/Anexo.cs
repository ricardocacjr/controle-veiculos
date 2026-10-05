using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>
/// Arquivo guardado no banco: nota da oficina (manutenção), cupom do posto (abastecimento)...
/// Fica no banco porque o disco do servidor (Render) é apagado a cada deploy.
/// </summary>
public class Anexo : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }
    public required string Nome { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Conteudo { get; set; }
}
