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
/// </summary>
public class GoogleVisionOdometerOcrService(IConfiguration configuration, ILogger<GoogleVisionOdometerOcrService> logger)
    : IOdometerOcrService
{
    private static readonly Regex DigitRun = new(@"\d{3,7}", RegexOptions.Compiled);

    // "80.007" / "80 007" (milhar separado) → "80007" antes de procurar os números.
    private static readonly Regex SeparadorMilhar = new(@"(?<=\d)[.\s](?=\d{3}(?!\d))", RegexOptions.Compiled);

    /// <summary>Folga máxima entre o último km conhecido e a leitura (uso sem app, erro de cadastro).</summary>
    private const int FolgaMaximaKm = 20000;

    public async Task<int?> ExtractOdometerAsync(Stream imageStream, int? kmReferencia, CancellationToken ct = default)
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
            return string.IsNullOrWhiteSpace(texto) ? null : EscolherOdometro(texto, kmReferencia);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler odômetro via Google Cloud Vision.");
            return null;
        }
    }

    /// <summary>
    /// Painéis mostram vários números (velocímetro, relógio "18 40", autonomia "152 km"). Com o
    /// último km conhecido, fica o menor número ≥ a ele (o odômetro só sobe). Sem referência
    /// (veículo recém-cadastrado com km 0), cai na heurística antiga: a sequência mais longa.
    /// </summary>
    internal static int? EscolherOdometro(string texto, int? kmReferencia)
    {
        var candidatos = DigitRun.Matches(SeparadorMilhar.Replace(texto, ""))
            .Select(m => int.TryParse(m.Value, out var n) ? n : (int?)null)
            .OfType<int>()
            .Distinct()
            .ToList();

        if (kmReferencia is > 0 and var referencia)
        {
            return candidatos
                .Where(n => n >= referencia && n <= referencia + FolgaMaximaKm)
                .OrderBy(n => n - referencia)
                .Select(n => (int?)n)
                .FirstOrDefault();
        }

        return candidatos
            .OrderByDescending(n => n.ToString().Length)
            .ThenByDescending(n => n)
            .Select(n => (int?)n)
            .FirstOrDefault();
    }
}
