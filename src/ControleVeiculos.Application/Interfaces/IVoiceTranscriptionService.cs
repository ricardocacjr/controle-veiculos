namespace ControleVeiculos.Application.Interfaces;

public interface IVoiceTranscriptionService
{
    /// <summary>
    /// Transcreve um áudio para texto. Retorna null se o serviço não estiver configurado ou a
    /// transcrição falhar — quem chama decide como tratar (ex: marcar a nota como "Falhou" e
    /// deixar pra reprocessar depois).
    /// </summary>
    Task<string?> TranscribeAsync(Stream audioStream, string contentType, CancellationToken ct = default);
}
