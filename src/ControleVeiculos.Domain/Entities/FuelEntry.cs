using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

public class FuelEntry : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required Guid UsoId { get; set; }
    public UsageRecord? Uso { get; set; }

    public decimal Litros { get; set; }
    public decimal ValorTotal { get; set; }
    public int Odometro { get; set; }
}
