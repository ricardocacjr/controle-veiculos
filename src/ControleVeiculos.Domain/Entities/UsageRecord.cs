using ControleVeiculos.Domain.Common;
using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Domain.Entities;

/// <summary>A single vehicle checkout: from start (odometer/photos) to end (odometer/photos).</summary>
public class UsageRecord : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required Guid VeiculoId { get; set; }
    public Vehicle? Veiculo { get; set; }

    public required Guid MotoristaId { get; set; }
    public Driver? Motorista { get; set; }

    public required string Finalidade { get; set; }
    public string? Origem { get; set; }
    public string? Destino { get; set; }

    public int OdometroInicial { get; set; }
    public int? OdometroFinal { get; set; }

    public DateTimeOffset IniciadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinalizadoEm { get; set; }

    public UsageRecordStatus Status { get; set; } = UsageRecordStatus.EmAndamento;

    public ICollection<VehiclePhoto> Fotos { get; set; } = new List<VehiclePhoto>();
    public ICollection<VoiceNote> NotasDeVoz { get; set; } = new List<VoiceNote>();
    public ICollection<FuelEntry> Abastecimentos { get; set; } = new List<FuelEntry>();
}
