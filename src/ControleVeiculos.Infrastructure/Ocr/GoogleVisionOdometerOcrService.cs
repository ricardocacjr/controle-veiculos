using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ControleVeiculos.Application.Interfaces;

namespace ControleVeiculos.Infrastructure.Ocr;

/// <summary>
/// Lê o odômetro numa foto via Google Cloud Vision (TEXT_DETECTION). Requer a mesma conta de
/// serviço usada pra transcrição de voz, com a API "Cloud Vision" também habilitada — ver
/// README, seção "Configuração de voz e imagem (Google Cloud)".
///
/// Heurística: o odômetro costuma ser a sequência de dígitos mais longa detectada na imagem
/// (painéis costumam ter outros números menores — velocímetro, relógio, etc). Não é perfeito;
/// por isso é sempre devolvido como sugestão pra conferência humana, nunca aplicado direto.
/// </summary>
public class GoogleVisionOdometerOcrService(IConfiguration configuration, ILogger<GoogleVisionOdometerOcrService> logger)
    : IOdometerOcrService
{
    private static readonly Regex DigitRun = new(@"\d{3,7}", RegexOptions.Compiled);

    public async Task<int?> ExtractOdometerAsync(Stream imageStream, CancellationToken ct = default)
    {
        var credentialsPath = configuration["GoogleCloud:CredentialsPath"];
        if (string.IsNullOrWhiteSpace(credentialsPath) && Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") is null)
        {
            logger.LogWarning("Leitura de odômetro pulada: credenciais do Google Cloud não configuradas.");
            return null;
        }

        try
        {
            var builder = new ImageAnnotatorClientBuilder();
            if (!string.IsNullOrWhiteSpace(credentialsPath))
                builder.GoogleCredential = await GoogleCredential.FromFileAsync(credentialsPath, ct);

            var client = await builder.BuildAsync(ct);

            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, ct);
            var image = Image.FromBytes(memoryStream.ToArray());

            var response = await client.DetectTextAsync(image);
            var texto = response.FirstOrDefault()?.Description;
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var candidato = DigitRun.Matches(texto)
                .Select(m => m.Value)
                .OrderByDescending(v => v.Length)
                .FirstOrDefault();

            return candidato is not null && int.TryParse(candidato, out var odometro) ? odometro : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler odômetro via Google Cloud Vision.");
            return null;
        }
    }
}
