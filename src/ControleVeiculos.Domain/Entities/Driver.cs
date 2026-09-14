using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

public class Driver : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required string Nome { get; set; }
    public required string Cnh { get; set; }
    public DateOnly? CnhValidade { get; set; }
    public string? Telefone { get; set; }

    /// <summary>Links to the Identity user this driver logs in as (mobile app login).</summary>
    public Guid? UserId { get; set; }

    public ICollection<UsageRecord> Usos { get; set; } = new List<UsageRecord>();
}
