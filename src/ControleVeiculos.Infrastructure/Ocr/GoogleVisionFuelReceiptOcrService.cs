using System.Globalization;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ControleVeiculos.Application.Interfaces;

namespace ControleVeiculos.Infrastructure.Ocr;

/// <summary>
/// Lê litros/valor total/valor por litro de uma foto de nota fiscal ou cupom de abastecimento,
/// via Google Cloud Vision (mesma credencial da leitura de odômetro e da transcrição de voz).
///
/// Heurística por linha de texto (cupons fiscais brasileiros variam bastante de layout, então
/// isso é "melhor esforço", não garantido). Pra cada palavra-chave, procura o valor na mesma
/// linha ou nas duas seguintes (rótulo e valor costumam vir em linhas separadas):
/// - "LITRO" ou "QTDE"/"QUANT" → número com vírgula/ponto decimal são os litros.
/// - "UNIT" ou "PREÇO"/"PRECO" (preço unitário) → valor em R$ é o valor/litro.
/// - "TOTAL" (mas não "SUBTOTAL") → valor em R$ é o valor total.
/// - Se valor total não for achado por palavra-chave, usa o maior valor em R$ do cupom inteiro
///   (geralmente é o total mesmo).
/// </summary>
public class GoogleVisionFuelReceiptOcrService(IConfiguration configuration, ILogger<GoogleVisionFuelReceiptOcrService> logger)
    : IFuelReceiptOcrService
{
    private static readonly Regex MoneyPattern = new(@"(\d{1,4}[.,]\d{2})", RegexOptions.Compiled);
    private static readonly Regex QuantityPattern = new(@"(\d{1,3}[.,]\d{2,4})", RegexOptions.Compiled);

    public async Task<FuelReceiptReading> ExtractAsync(Stream imageStream, CancellationToken ct = default)
    {
        var credentialsPath = configuration["GoogleCloud:CredentialsPath"];
        if (string.IsNullOrWhiteSpace(credentialsPath) && Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") is null)
        {
            logger.LogWarning("Leitura de comprovante pulada: credenciais do Google Cloud não configuradas.");
            return new FuelReceiptReading(null, null, null);
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
                return new FuelReceiptReading(null, null, null);

            var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var litros = ParseFromLines(linhas, l => l.Contains("LITRO", StringComparison.OrdinalIgnoreCase)
                || l.Contains("QTDE", StringComparison.OrdinalIgnoreCase)
                || l.Contains("QUANT", StringComparison.OrdinalIgnoreCase), QuantityPattern);

            var valorPorLitro = ParseFromLines(linhas, l => l.Contains("UNIT", StringComparison.OrdinalIgnoreCase)
                || l.Contains("PREÇO", StringComparison.OrdinalIgnoreCase)
                || l.Contains("PRECO", StringComparison.OrdinalIgnoreCase), MoneyPattern);

            var valorTotal = ParseFromLines(linhas, l => l.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)
                && !l.Contains("SUBTOTAL", StringComparison.OrdinalIgnoreCase), MoneyPattern)
                ?? MoneyPattern.Matches(texto)
                    .Select(m => ParseDecimal(m.Value))
                    .Where(v => v is not null)
                    .OrderByDescending(v => v)
                    .FirstOrDefault();

            return new FuelReceiptReading(litros, valorTotal, valorPorLitro);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler comprovante de abastecimento via Google Cloud Vision.");
            return new FuelReceiptReading(null, null, null);
        }
    }

    /// <summary>
    /// Procura o valor perto de uma linha com a palavra-chave — não necessariamente na mesma
    /// linha: cupons fiscais costumam quebrar rótulo e valor em linhas separadas (rótulo,
    /// depois valor logo abaixo), então olha a própria linha e as duas seguintes.
    /// </summary>
    private static decimal? ParseFromLines(string[] linhas, Func<string, bool> matchLinha, Regex valuePattern)
    {
        var indice = Array.FindIndex(linhas, l => matchLinha(l));
        if (indice < 0)
            return null;

        for (var i = indice; i < Math.Min(indice + 3, linhas.Length); i++)
        {
            var match = valuePattern.Match(linhas[i]);
            if (match.Success)
                return ParseDecimal(match.Value);
        }

        return null;
    }

    private static decimal? ParseDecimal(string value) =>
        decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
}
