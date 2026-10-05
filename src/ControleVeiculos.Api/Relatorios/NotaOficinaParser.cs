using System.Globalization;
using System.Text.RegularExpressions;
using ControleVeiculos.Shared.Manutencoes;

namespace ControleVeiculos.Api.Relatorios;

/// <summary>Campos lidos da nota da oficina (texto do PDF ou OCR da foto).</summary>
public record NotaOficina(DateOnly? Data, int? Km, string? Placa, string? Oficina, List<ManutencaoItemDto> Itens,
    decimal? MaoDeObra, decimal? Total, string? Observacao);

/// <summary>
/// Lê a nota no formato das oficinas ("QUANTIDADE; DESCRIÇÃO; VALOR", "MÃO DE OBRA: R$ 200,00",
/// "TOTAL: R$ 787,00"...). Melhor esforço: o que não for achado a pessoa preenche na tela.
/// </summary>
public static class NotaOficinaParser
{
    private const RegexOptions Opcoes = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Dinheiro = @"(\d{1,3}(?:\.\d{3})*,\d{2}|\d+,\d{2})";

    private static readonly Regex DataRegex = new(@"DATA\s*:?\s*(\d{1,2}/\d{1,2}/\d{2,4})", Opcoes);
    private static readonly Regex QualquerData = new(@"\b(\d{1,2}/\d{1,2}/\d{4})\b", Opcoes);
    private static readonly Regex KmRegex = new(@"\bKM\s*:?\s*(\d{1,3}(?:\.\d{3})+|\d{4,7})\b", Opcoes);
    private static readonly Regex PlacaRegex = new(@"PLACA\s*:?\s*([A-Z]{3}-?\d[A-Z0-9]\d{2})", Opcoes);
    private static readonly Regex MaoDeObraRegex = new(@"M[ÃA]O\s+DE\s+OBRA\s*:?\s*(?:R\$)?\s*" + Dinheiro, Opcoes);
    private static readonly Regex TotalRegex = new(@"(?<!SUB)TOTAL\s*(?:GERAL)?\s*:?\s*(?:R\$)?\s*" + Dinheiro, Opcoes);
    private static readonly Regex ObservacaoRegex = new(@"OBSERVA[ÇC][ÃA]O\s*:?\s*(.*)", Opcoes);
    private static readonly Regex EnderecoRegex = new(@"^\s*((?:RUA|AV\.?|AVENIDA|ROD\.?|RODOVIA|ALAMEDA|TRAVESSA)\s*:?\s*.+)$", Opcoes);
    private static readonly Regex NomeOficinaRegex = new(@"(MEC[AÂ]NICA|OFICINA|AUTO\s*CENTER|CENTRO\s+AUTOMOTIVO|AUTO\s*PE[ÇC]AS)", Opcoes);

    // "03  Litros de óleo R$ 126,00" (quantidade opcional; valor = total da linha).
    private static readonly Regex ItemRegex = new(@"^\s*(?:(\d{1,3}(?:[.,]\d{1,2})?)\s+)?(.+?)\s+R\$\s*" + Dinheiro + @"\s*$", Opcoes);
    private static readonly Regex NaoEhItem = new(@"M[ÃA]O\s+DE\s+OBRA|TOTAL|DESCONTO|VALOR\s+PAGO|TROCO|ACR[EÉ]SCIMO", Opcoes);

    public static NotaOficina Interpretar(string texto)
    {
        var linhas = texto.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var itens = new List<ManutencaoItemDto>();
        foreach (var linha in linhas)
        {
            if (NaoEhItem.IsMatch(linha) || ItemRegex.Match(linha) is not { Success: true } m)
                continue;
            var descricao = Regex.Replace(m.Groups[2].Value, @"\s+", " ").Trim(' ', ';', '-', ':');
            if (descricao.Length < 2)
                continue;
            var quantidade = m.Groups[1].Success ? Numero(m.Groups[1].Value) ?? 1 : 1;
            itens.Add(new ManutencaoItemDto(quantidade, descricao, Numero(m.Groups[3].Value) ?? 0));
        }

        return new NotaOficina(
            Data(texto),
            KmRegex.Match(texto) is { Success: true } km && int.TryParse(km.Groups[1].Value.Replace(".", ""), out var k) && k >= 1000 ? k : null,
            PlacaRegex.Match(texto) is { Success: true } p ? p.Groups[1].Value.ToUpperInvariant() : null,
            Oficina(linhas),
            itens,
            MaoDeObraRegex.Match(texto) is { Success: true } mo ? Numero(mo.Groups[1].Value) : null,
            TotalRegex.Matches(texto).LastOrDefault() is { } t ? Numero(t.Groups[1].Value) : null,
            linhas.Select(l => ObservacaoRegex.Match(l)).FirstOrDefault(o => o.Success && o.Groups[1].Value.Trim().Length > 1)?.Groups[1].Value.Trim());
    }

    private static DateOnly? Data(string texto)
    {
        var bruto = DataRegex.Match(texto) is { Success: true } d ? d.Groups[1].Value
            : QualquerData.Match(texto) is { Success: true } q ? q.Groups[1].Value : null;
        if (bruto is null)
            return null;
        string[] formatos = ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy"];
        return DateOnly.TryParseExact(bruto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;
    }

    /// <summary>Nome da oficina se aparecer ("MECÂNICA SILVA"); senão o endereço dela ("RUA: Agostinho Brusamolin, 70").</summary>
    private static string? Oficina(List<string> linhas)
    {
        var nome = linhas.FirstOrDefault(l => NomeOficinaRegex.IsMatch(l) && !l.Contains("R$"));
        if (nome is not null)
            return Limpar(nome).ToUpperInvariant();
        var endereco = linhas.Select(l => EnderecoRegex.Match(l)).FirstOrDefault(m => m.Success);
        return endereco is null ? null : Limpar(Regex.Replace(endereco.Groups[1].Value, @"^\s*RUA\s*:\s*", "RUA ", Opcoes)).ToUpperInvariant();
    }

    private static string Limpar(string texto) => Regex.Replace(texto, @"\s+", " ").Trim(' ', ';', ':');

    private static decimal? Numero(string valor) =>
        decimal.TryParse(valor.Replace(".", "").Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
}
