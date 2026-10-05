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
    private static readonly Regex QuantityPattern = new(@"(\d{1,3}[.,]\d{2,4})", RegexOptions.Compiled);

    // Preço do litro vem com 3 casas nos postos ("R$ 5,879") — com o padrão de dinheiro (2 casas)
    // virava 5,87.
    private static readonly Regex UnitPricePattern = new(@"(\d{1,2}[.,]\d{2,3})(?!\d)", RegexOptions.Compiled);

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

            return Interpretar(texto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler comprovante de abastecimento via Google Cloud Vision.");
            return new FuelReceiptReading(null, null, null);
        }
    }

    // Número "solto" com 2 ou 3 casas: não pode ser pedaço de outro número (CNPJ 11.600.005,
    // encerrante 817057,944, chave de acesso...).
    private static readonly Regex NumeroPattern = new(@"(?<![\d.,])\d{1,4}[.,]\d{2,3}(?!\d|[.,]\d)", RegexOptions.Compiled);

    // Linha do item no cupom: "19,416 L x 6,890 133,77" (litros × preço do litro = total).
    private static readonly Regex ItemPattern = new(
        @"(?<![\d.,])(\d{1,3}[.,]\d{2,3})\s*(?:L|LT|LTS|LITROS?)?\s*[xX×*]\s*(\d{1,2}[.,]\d{2,4})(?:\s+(?:R\$\s*)?(\d{1,4}[.,]\d{2})(?!\d))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const decimal PrecoMinimo = 2.5m;
    private const decimal PrecoMaximo = 15m;

    /// <summary>
    /// Interpreta o texto lido do cupom. Ordem (da mais confiável pra menos):
    /// 1. linha do item "litros x preço total";
    /// 2. três números do cupom que fecham a conta (litros × preço ≈ total);
    /// 3. palavras-chave ("VALOR PAGO", "TOTAL R$", "QTDE"...), e o que faltar é calculado dos outros dois.
    /// Antes, "Valor unit." / "Valor total" do CABEÇALHO da tabela eram tomados como rótulo e o
    /// primeiro número da linha de baixo (os litros) virava preço e total.
    /// </summary>
    public static FuelReceiptReading Interpretar(string texto)
    {
        var leitura = InterpretarValores(texto);
        return leitura with { Desconto = Desconto(texto, leitura.ValorTotal) };
    }

    /// <summary>
    /// Desconto do app do posto (pagou pelo app: o print mostra "Desconto R$ 13,77" ou o total e o
    /// valor pago). Pelo rótulo; sem rótulo, a diferença entre o valor na bomba e o valor pago.
    /// </summary>
    private static decimal? Desconto(string texto, decimal? bomba)
    {
        var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var desconto = ValorNaLinha(linhas, "DESCONTO") ?? ValorNaLinha(linhas, "DESC.") ?? ValorNaLinha(linhas, "ECONOMI")
            ?? ValorNaLinha(linhas, "ABATIMENTO");
        if (desconto is null && bomba is > 0
            && (ValorNaLinha(linhas, "VALOR PAGO") ?? ValorNaLinha(linhas, "TOTAL PAGO") ?? ValorNaLinha(linhas, "VOCÊ PAGOU") ?? ValorNaLinha(linhas, "VOCE PAGOU")) is { } pago
            && pago < bomba)
            desconto = bomba - pago;
        return desconto is > 0 && (bomba is null || desconto < bomba) ? desconto : null;
    }

    private static FuelReceiptReading InterpretarValores(string texto)
    {
        var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // 1. Linha do item (às vezes o OCR quebra em duas linhas — tenta também juntando com a seguinte).
        for (var i = 0; i < linhas.Length; i++)
        {
            foreach (var trecho in new[] { linhas[i], i + 1 < linhas.Length ? linhas[i] + " " + linhas[i + 1] : null })
            {
                if (trecho is null || ItemPattern.Match(trecho) is not { Success: true } m)
                    continue;
                var l = ParseDecimal(m.Groups[1].Value);
                var p = ParseDecimal(m.Groups[2].Value);
                var t = m.Groups[3].Success ? ParseDecimal(m.Groups[3].Value) : null;
                if (l is not > 0 || p is not (>= PrecoMinimo and <= PrecoMaximo))
                    continue;
                if (t is { } total && FechaConta(l.Value, p.Value, total))
                    return new FuelReceiptReading(l, total, p);
                if (t is null)
                    return new FuelReceiptReading(l, Math.Round(l.Value * p.Value, 2), p);
            }
        }

        // 2. Três números que fecham a conta.
        var numeros = NumeroPattern.Matches(texto)
            .Select(m => (Texto: m.Value, Valor: ParseDecimal(m.Value)))
            .Where(n => n.Valor is > 0)
            .Select(n => (n.Texto, Valor: n.Valor!.Value, Casas: n.Texto.Length - n.Texto.IndexOfAny(['.', ',']) - 1))
            .Distinct()
            .ToList();

        var melhor = (from l in numeros
                      where l.Valor is >= 0.5m and <= 500m
                      from p in numeros
                      where p.Valor is >= PrecoMinimo and <= PrecoMaximo && p.Texto != l.Texto
                      from t in numeros
                      where t.Casas == 2 && t.Texto != l.Texto && t.Texto != p.Texto && FechaConta(l.Valor, p.Valor, t.Valor)
                      // Litros com 3 casas (padrão da bomba) e total maior pesam a favor.
                      orderby Math.Abs(l.Valor * p.Valor - t.Valor), l.Casas == 3 ? 0 : 1, t.Valor descending
                      select (Litros: l.Valor, Preco: p.Valor, Total: t.Valor))
                     .FirstOrDefault();
        if (melhor.Total > 0)
            return new FuelReceiptReading(melhor.Litros, melhor.Total, melhor.Preco);

        // 3. Palavras-chave.
        var litros = ParseFromLines(linhas, l => l.Contains("LITRO", StringComparison.OrdinalIgnoreCase)
            || l.Contains("QTDE", StringComparison.OrdinalIgnoreCase)
            || l.Contains("QUANT", StringComparison.OrdinalIgnoreCase), QuantityPattern);

        var valorPorLitro = ParseFromLines(linhas, l => l.Contains("UNIT", StringComparison.OrdinalIgnoreCase)
            || l.Contains("PREÇO", StringComparison.OrdinalIgnoreCase)
            || l.Contains("PRECO", StringComparison.OrdinalIgnoreCase), UnitPricePattern);
        if (valorPorLitro is not (>= PrecoMinimo and <= PrecoMaximo))
            valorPorLitro = null;

        // Valor na bomba primeiro (o pago pode ter desconto do app).
        var valorTotal = ValorNaLinha(linhas, "VALOR TOTAL")
            ?? ValorNaLinha(linhas, "TOTAL R$")
            ?? ValorNaLinha(linhas, "A PAGAR")
            ?? ValorNaLinha(linhas, "VALOR PAGO")
            ?? numeros.Where(n => n.Casas == 2).Select(n => (decimal?)n.Valor).OrderByDescending(v => v).FirstOrDefault();

        // Dois dos três certos → o terceiro sai da conta.
        if (litros is > 0 && valorTotal is > 0 && valorPorLitro is null)
        {
            var preco = Math.Round(valorTotal.Value / litros.Value, 3);
            valorPorLitro = preco is >= PrecoMinimo and <= PrecoMaximo ? preco : null;
        }
        else if (litros is null && valorTotal is > 0 && valorPorLitro is > 0)
        {
            litros = Math.Round(valorTotal.Value / valorPorLitro.Value, 3);
        }

        return new FuelReceiptReading(litros, valorTotal, valorPorLitro);
    }

    /// <summary>Litros × preço bate com o total (tolerância de arredondamento do cupom).</summary>
    private static bool FechaConta(decimal litros, decimal preco, decimal total) =>
        Math.Abs(litros * preco - total) <= Math.Max(0.05m, total * 0.005m);

    /// <summary>Valor com 2 casas na linha do rótulo (o último número dela) ou na linha seguinte.</summary>
    private static decimal? ValorNaLinha(string[] linhas, string rotulo)
    {
        for (var i = 0; i < linhas.Length; i++)
        {
            if (!linhas[i].Contains(rotulo, StringComparison.OrdinalIgnoreCase))
                continue;
            for (var j = i; j < Math.Min(i + 2, linhas.Length); j++)
            {
                var valores = NumeroPattern.Matches(linhas[j])
                    .Where(m => m.Value.Length - m.Value.IndexOfAny(['.', ',']) - 1 == 2)
                    .Select(m => ParseDecimal(m.Value))
                    .ToList();
                if (valores.Count > 0)
                    return valores[^1];
            }
        }
        return null;
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
