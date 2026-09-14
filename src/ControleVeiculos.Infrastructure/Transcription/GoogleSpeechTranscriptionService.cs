using Google.Apis.Auth.OAuth2;
using Google.Cloud.Speech.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ControleVeiculos.Application.Interfaces;

namespace ControleVeiculos.Infrastructure.Transcription;

/// <summary>
/// Transcreve notas de voz via Google Cloud Speech-to-Text. Requer uma conta de serviço do GCP
/// com a API "Cloud Speech-to-Text" habilitada — ver README, seção "Configuração da
/// transcrição de voz". Usa reconhecimento síncrono (Recognize), que só suporta áudios de até
/// ~1 minuto; para notas mais longas seria necessário trocar para LongRunningRecognize.
/// </summary>
public class GoogleSpeechTranscriptionService(IConfiguration configuration, ILogger<GoogleSpeechTranscriptionService> logger)
    : IVoiceTranscriptionService
{
    public async Task<string?> TranscribeAsync(Stream audioStream, string contentType, CancellationToken ct = default)
    {
        var credentialsPath = configuration["GoogleCloud:CredentialsPath"];
        if (string.IsNullOrWhiteSpace(credentialsPath) && Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") is null)
        {
            logger.LogWarning("Transcrição de voz pulada: credenciais do Google Cloud não configuradas.");
            return null;
        }

        try
        {
            var builder = new SpeechClientBuilder();
            if (!string.IsNullOrWhiteSpace(credentialsPath))
                builder.GoogleCredential = await GoogleCredential.FromFileAsync(credentialsPath, ct);

            var speech = await builder.BuildAsync(ct);

            using var memoryStream = new MemoryStream();
            await audioStream.CopyToAsync(memoryStream, ct);

            var response = await speech.RecognizeAsync(new RecognitionConfig
            {
                Encoding = MapEncoding(contentType),
                LanguageCode = "pt-BR",
                EnableAutomaticPunctuation = true,
            }, RecognitionAudio.FromBytes(memoryStream.ToArray()));

            var texto = string.Join(" ", response.Results.Select(r => r.Alternatives.FirstOrDefault()?.Transcript));
            return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao transcrever nota de voz via Google Cloud Speech-to-Text.");
            return null;
        }
    }

    private static RecognitionConfig.Types.AudioEncoding MapEncoding(string contentType) => contentType switch
    {
        "audio/wav" or "audio/x-wav" => RecognitionConfig.Types.AudioEncoding.Linear16,
        "audio/flac" => RecognitionConfig.Types.AudioEncoding.Flac,
        "audio/ogg" or "audio/opus" => RecognitionConfig.Types.AudioEncoding.OggOpus,
        "audio/webm" => RecognitionConfig.Types.AudioEncoding.WebmOpus,
        "audio/amr" => RecognitionConfig.Types.AudioEncoding.Amr,
        _ => RecognitionConfig.Types.AudioEncoding.EncodingUnspecified,
    };
}
