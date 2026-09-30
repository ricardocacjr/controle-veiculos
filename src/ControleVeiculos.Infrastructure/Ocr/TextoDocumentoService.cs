using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using ControleVeiculos.Application.Interfaces;

namespace ControleVeiculos.Infrastructure.Ocr;

/// <summary>
/// PDF gerado pela oficina tem o texto dentro (PdfPig lê direto, sem OCR). Foto da nota vai pro
/// Google Cloud Vision (mesma credencial da leitura do painel e do cupom).
/// </summary>
public class TextoDocumentoService(IConfiguration configuration, ILogger<TextoDocumentoService> logger) : ITextoDocumentoService
{
    public async Task<string?> ExtrairTextoAsync(byte[] conteudo, bool ehPdf, CancellationToken ct = default)
    {
        try
        {
            return ehPdf ? LerPdf(conteudo) : await LerImagemAsync(conteudo, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler o texto da nota ({Tipo}).", ehPdf ? "PDF" : "imagem");
            return null;
        }
    }

    private static string LerPdf(byte[] conteudo)
    {
        using var doc = PdfDocument.Open(conteudo);
        var sb = new StringBuilder();
        foreach (var pagina in doc.GetPages())
            sb.AppendLine(ContentOrderTextExtractor.GetText(pagina));
        return sb.ToString();
    }

    private async Task<string?> LerImagemAsync(byte[] conteudo, CancellationToken ct)
    {
        var credentialsPath = configuration["GoogleCloud:CredentialsPath"];
        if (string.IsNullOrWhiteSpace(credentialsPath) && Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") is null)
        {
            logger.LogWarning("Leitura de nota pulada: credenciais do Google Cloud não configuradas.");
            return null;
        }

        var builder = new ImageAnnotatorClientBuilder();
        if (!string.IsNullOrWhiteSpace(credentialsPath))
            builder.GoogleCredential = await GoogleCredential.FromFileAsync(credentialsPath, ct);
        var client = await builder.BuildAsync(ct);

        // DocumentText mantém melhor as linhas de uma nota/tabela do que o TextDetection.
        var resposta = await client.DetectDocumentTextAsync(Image.FromBytes(conteudo));
        return resposta?.Text;
    }
}
