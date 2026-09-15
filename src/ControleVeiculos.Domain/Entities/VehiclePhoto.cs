using ControleVeiculos.Domain.Common;
using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Domain.Entities;

public class VehiclePhoto : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required Guid UsoId { get; set; }
    public UsageRecord? Uso { get; set; }

    public VehiclePhotoType Tipo { get; set; } = VehiclePhotoType.Geral;

    /// <summary>Storage key/URL of the uploaded file (blob storage or local disk path).</summary>
    public required string ArquivoUrl { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Leitura sugerida por OCR (Google Cloud Vision) pra fotos de odômetro — sempre uma
    /// sugestão pra conferência humana, nunca aplicada automaticamente ao odômetro do veículo.</summary>
    public int? OdometroLido { get; set; }
}
