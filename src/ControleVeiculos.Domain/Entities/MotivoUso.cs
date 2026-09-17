using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>Catálogo de finalidades/motivos de uso do veículo (ex: "Entrega de móveis",
/// "Traslado pessoal") — cresce sozinho: toda vez que um uso é iniciado com uma finalidade nova,
/// ela entra aqui automaticamente pra aparecer como sugestão da próxima vez.</summary>
public class MotivoUso : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required string Nome { get; set; }
}
