using ControleVeiculos.Domain.Enums;

namespace ControleVeiculos.Shared.UsageRecords;

public record VoiceNoteDto(Guid Id, string ArquivoUrl, string? TranscricaoTexto, VoiceNoteStatus Status, DateTimeOffset CreatedAt);
