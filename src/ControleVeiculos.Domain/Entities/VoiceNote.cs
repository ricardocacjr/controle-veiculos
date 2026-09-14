using ControleVeiculos.Domain.Common;
using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Domain.Entities;

public class VoiceNote : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required Guid UsoId { get; set; }
    public UsageRecord? Uso { get; set; }

    /// <summary>Storage key/URL of the uploaded audio file.</summary>
    public required string ArquivoUrl { get; set; }

    public string? TranscricaoTexto { get; set; }
    public VoiceNoteStatus Status { get; set; } = VoiceNoteStatus.Pendente;
}
