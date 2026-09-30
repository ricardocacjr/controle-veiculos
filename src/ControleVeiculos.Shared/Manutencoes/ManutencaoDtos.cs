using System.Globalization;
using System.Text;

namespace ControleVeiculos.Shared.Manutencoes;

public record ManutencaoItemDto(decimal Quantidade, string Descricao, decimal Valor);

public record ManutencaoProximaDto(string Tipo, int? ProximaKm, DateOnly? ProximaData);

public record ManutencaoDto(
    Guid Id,
    Guid VeiculoId,
    string VeiculoPlaca,
    DateOnly Data,
    int Km,
    string Tipo,
    string? Observacao,
    decimal Valor,
    decimal MaoDeObra,
    string? Oficina,
    IReadOnlyList<ManutencaoItemDto> Itens,
    IReadOnlyList<ManutencaoProximaDto> Proximas,
    bool TemAnexo);

public record SalvarManutencaoRequest(
    Guid VeiculoId,
    DateOnly Data,
    int Km,
    string? Oficina,
    string? Observacao,
    decimal MaoDeObra,
    IReadOnlyList<ManutencaoItemDto> Itens,
    IReadOnlyList<ManutencaoProximaDto> Proximas,
    Guid? AnexoId = null);

/// <summary>O que o app conseguiu ler da nota da oficina (PDF ou foto). O anexo já fica guardado.</summary>
public record LeituraNotaDto(
    Guid AnexoId,
    DateOnly? Data,
    int? Km,
    string? Placa,
    string? Oficina,
    IReadOnlyList<ManutencaoItemDto> Itens,
    decimal? MaoDeObra,
    decimal? Total,
    string? Observacao,
    bool LeuAlgo);

public enum SituacaoManutencao { EmDia = 0, EmBreve = 1, Vencida = 2 }

/// <summary>A última manutenção de cada categoria que tem "próxima" marcada, com quanto falta.</summary>
public record ProximaManutencaoDto(
    Guid VeiculoId,
    string Veiculo,
    string Tipo,
    int? ProximaKm,
    DateOnly? ProximaData,
    int? FaltamKm,
    int? FaltamDias,
    SituacaoManutencao Situacao,
    Guid UltimaManutencaoId);

/// <summary>Resumo de um carro: km atual, gasto e custo por km dos últimos 12 meses, próximas manutenções.</summary>
public record ManutencaoResumoDto(
    Guid VeiculoId,
    string Veiculo,
    int OdometroAtual,
    decimal Gasto12Meses,
    int KmRodados12Meses,
    decimal? CustoPorKmSemPneus,
    IReadOnlyList<ProximaManutencaoDto> Proximas);

/// <summary>
/// Reconhece a categoria de um item da nota ("03 Litros de óleo" → TROCA DE ÓLEO, "Filtro de ar" →
/// FILTROS...) e o intervalo usual até a próxima. Usado na Api (resumo, avisos) e na tela (sugestões).
/// </summary>
public static class CategoriasManutencao
{
    public const string Oleo = "TROCA DE ÓLEO";
    public const string Filtros = "FILTROS";
    public const string Pneus = "PNEUS";
    public const string Outros = "OUTROS";

    /// <summary>Categoria → intervalo usual (km, meses; 0 = não se aplica). Referência de mercado, o admin ajusta.</summary>
    public static readonly IReadOnlyDictionary<string, (int Km, int Meses)> Intervalos = new Dictionary<string, (int, int)>
    {
        [Oleo] = (10000, 12),
        [Filtros] = (10000, 12),
        ["REVISÃO"] = (10000, 12),
        [Pneus] = (40000, 0),
        ["FREIOS"] = (20000, 0),
        ["ALINHAMENTO E BALANCEAMENTO"] = (10000, 0),
        ["CORREIA DENTADA"] = (50000, 48),
        ["VELAS"] = (30000, 0),
        ["ARREFECIMENTO"] = (40000, 24),
        ["BATERIA"] = (0, 24),
        ["PALHETAS"] = (0, 12),
        ["HIGIENIZAÇÃO"] = (0, 12),
    };

    // Ordem importa: "filtro de óleo" é troca de óleo (vem junto), "filtro de ar" é filtro.
    private static readonly (string Chave, string Categoria)[] Regras =
    [
        ("OLEO", Oleo),
        ("FILTRO", Filtros),
        ("PNEU", Pneus),
        ("PASTILHA", "FREIOS"), ("FREIO", "FREIOS"), ("DISCO", "FREIOS"), ("LONA", "FREIOS"),
        ("ALINHAMENTO", "ALINHAMENTO E BALANCEAMENTO"), ("BALANCEAMENTO", "ALINHAMENTO E BALANCEAMENTO"),
        ("CORREIA", "CORREIA DENTADA"),
        ("VELA", "VELAS"),
        ("ADITIVO", "ARREFECIMENTO"), ("RADIADOR", "ARREFECIMENTO"),
        ("BATERIA", "BATERIA"),
        ("PALHETA", "PALHETAS"),
        ("HIGIENIZ", "HIGIENIZAÇÃO"),
        ("REVISAO", "REVISÃO"),
    ];

    /// <summary>Categoria do item, ou null se não for de controle periódico (ex.: fechadura).</summary>
    public static string? Classificar(string? descricao)
    {
        var texto = SemAcento(descricao ?? "").ToUpperInvariant();
        foreach (var (chave, categoria) in Regras)
            if (texto.Contains(chave))
                return categoria;
        return null;
    }

    /// <summary>Resumo da nota: categorias na ordem dos itens (+ OUTROS se sobrar item sem categoria).</summary>
    public static string Resumo(IEnumerable<ManutencaoItemDto> itens)
    {
        var lista = itens.ToList();
        var categorias = lista.Select(i => Classificar(i.Descricao)).Where(c => c is not null).Distinct().ToList();
        if (categorias.Count == 0)
            return lista.Count == 1 ? lista[0].Descricao.Trim().ToUpperInvariant() : lista.Count == 0 ? "MANUTENÇÃO" : Outros;
        if (lista.Any(i => Classificar(i.Descricao) is null))
            categorias.Add(Outros);
        var resumo = string.Join(", ", categorias);
        return resumo.Length <= 200 ? resumo : resumo[..200];
    }

    /// <summary>Próximas sugeridas pelos itens, a partir do km e da data desta manutenção.</summary>
    public static List<ManutencaoProximaDto> Sugerir(IEnumerable<ManutencaoItemDto> itens, int km, DateOnly data) =>
        itens.Select(i => Classificar(i.Descricao))
            .Where(c => c is not null && Intervalos.ContainsKey(c))
            .Distinct()
            .Select(c =>
            {
                var (ik, im) = Intervalos[c!];
                return new ManutencaoProximaDto(c!, ik > 0 && km > 0 ? km + ik : null, im > 0 ? data.AddMonths(im) : null);
            })
            .ToList();

    private static string SemAcento(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}

/// <summary>Link temporário (assinado) pra abrir a nota da oficina no navegador.</summary>
public record LinkAnexoDto(string Url);
