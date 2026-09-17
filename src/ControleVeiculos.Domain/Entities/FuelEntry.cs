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

    /// <summary>Preço por litro no momento do abastecimento — lido do comprovante quando
    /// possível (ver <see cref="ControleVeiculos.Application.Interfaces.IFuelReceiptOcrService"/>),
    /// senão calculável a partir de ValorTotal/Litros.</summary>
    public decimal? ValorPorLitro { get; set; }
}
