using ControleVeiculos.Domain.Common;
using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Domain.Entities;

public class Vehicle : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required string Placa { get; set; }
    public required string Marca { get; set; }
    public required string Modelo { get; set; }
    public int Ano { get; set; }
    public string? Cor { get; set; }
    public int OdometroAtual { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Disponivel;

    public ICollection<UsageRecord> Usos { get; set; } = new List<UsageRecord>();
}
