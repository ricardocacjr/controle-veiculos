using ClosedXML.Excel;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Domain.Enums;
using ControleVeiculos.Shared.Relatorios;

namespace ControleVeiculos.Api.Relatorios;

/// <summary>Planilha do período: resumo + uma aba por visão, cada uma como tabela do Excel (filtro e ordenação prontos).</summary>
public static class RelatorioExcel
{
    private const string FormatoKm = "#,##0";
    private const string FormatoReais = "\"R$\" #,##0.00";
    private const string FormatoLitros = "#,##0.00";
    private const string FormatoDataHora = "dd/mm/yyyy hh:mm";

    public static byte[] Gerar(RelatorioDto rel, IReadOnlyList<UsageRecord> saidasDoPeriodo)
    {
        using var wb = new XLWorkbook();

        Resumo(wb, rel);
        Saidas(wb, saidasDoPeriodo);
        Grupos(wb, "Por motorista", "Motorista", rel.PorMotorista);
        Grupos(wb, "Por empresa", "Empresa", rel.PorEmpresa);
        Grupos(wb, "Por motivo", "Motivo", rel.PorMotivo);
        Abastecimentos(wb, saidasDoPeriodo);
        Consumo(wb, rel.Consumo);
        Alertas(wb, rel.Alertas);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Resumo(XLWorkbook wb, RelatorioDto rel)
    {
        var ws = wb.AddWorksheet("Resumo");
        ws.Cell(1, 1).Value = "Controle de Veículos — relatório de uso";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell(2, 1).Value = $"Período: {rel.De:dd/MM/yyyy} a {rel.Ate:dd/MM/yyyy}";

        var r = rel.Resumo;
        (string Nome, XLCellValue Valor, string? Formato)[] linhas =
        [
            ("Saídas", r.Saidas, null),
            ("Em andamento", r.EmAndamento, null),
            ("Km rodados", r.KmRodados, FormatoKm),
            ("Horas de uso", r.Horas, "#,##0.0"),
            ("Abastecimentos", r.Abastecimentos, null),
            ("Litros", r.Litros, FormatoLitros),
            ("Gasto com combustível", r.GastoCombustivel, FormatoReais),
            ("Alertas", rel.Alertas.Count, null),
        ];
        for (var i = 0; i < linhas.Length; i++)
        {
            ws.Cell(4 + i, 1).Value = linhas[i].Nome;
            ws.Cell(4 + i, 2).Value = linhas[i].Valor;
            if (linhas[i].Formato is { } f)
                ws.Cell(4 + i, 2).Style.NumberFormat.Format = f;
        }
        ws.Range(4, 1, 3 + linhas.Length, 1).Style.Font.SetBold();
        ws.Cell(5 + linhas.Length, 1).Value = "Km e horas contam só saídas encerradas.";
        ws.Cell(5 + linhas.Length, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        ws.Columns().AdjustToContents();
    }

    private static void Saidas(XLWorkbook wb, IReadOnlyList<UsageRecord> saidas)
    {
        string[] cab = ["Saída", "Chegada", "Horas", "Motorista", "Empresa", "Motivo", "Veículo", "Km saída", "Km chegada", "Km rodados", "Saindo de", "Situação", "Litros", "Combustível (R$)"];
        var linhas = saidas.OrderBy(u => u.IniciadoEm).Select(u => new XLCellValue[]
        {
            Local(u.IniciadoEm),
            u.FinalizadoEm is { } f ? Local(f) : Blank.Value,
            u.Status == UsageRecordStatus.Finalizado ? Math.Round(RelatorioCalculadora.Horas(u), 2) : Blank.Value,
            u.Motorista?.Nome ?? "?",
            u.Empresa?.Nome ?? "",
            u.Finalidade,
            RelatorioCalculadora.NomeVeiculo(u.Veiculo),
            u.OdometroInicial,
            u.OdometroFinal is { } kmFim ? kmFim : Blank.Value,
            u.Status == UsageRecordStatus.Finalizado ? RelatorioCalculadora.Km(u) : Blank.Value,
            u.Origem ?? "",
            u.Status switch { UsageRecordStatus.EmAndamento => "Em andamento", UsageRecordStatus.Finalizado => "Encerrada", _ => "Cancelada" },
            RelatorioCalculadora.Litros(u),
            RelatorioCalculadora.Gasto(u),
        });
        var ws = Tabela(wb, "Saídas", cab, linhas);
        Formatar(ws, [1, 2], FormatoDataHora);
        Formatar(ws, [3], "#,##0.00");
        Formatar(ws, [8, 9, 10], FormatoKm);
        Formatar(ws, [13], FormatoLitros);
        Formatar(ws, [14], FormatoReais);
        Ajustar(ws);
    }

    private static void Grupos(XLWorkbook wb, string aba, string nome, IReadOnlyList<GrupoDto> grupos)
    {
        string[] cab = [nome, "Saídas", "Km rodados", "Horas", "Combustível (R$)"];
        var ws = Tabela(wb, aba, cab, grupos.Select(g => new XLCellValue[] { g.Nome, g.Saidas, g.KmRodados, g.Horas, g.GastoCombustivel }));
        Formatar(ws, [3], FormatoKm);
        Formatar(ws, [4], "#,##0.0");
        Formatar(ws, [5], FormatoReais);
        Ajustar(ws);
    }

    private static void Abastecimentos(XLWorkbook wb, IReadOnlyList<UsageRecord> saidas)
    {
        string[] cab = ["Data", "Motorista", "Veículo", "Empresa", "Litros", "Valor (R$)", "R$/litro", "Km", "Mapa"];
        var linhas = saidas
            .SelectMany(u => u.Abastecimentos.Select(a => (u, a)))
            .OrderBy(x => x.a.CreatedAt)
            .Select(x => new XLCellValue[]
            {
                Local(x.a.CreatedAt),
                x.u.Motorista?.Nome ?? "?",
                RelatorioCalculadora.NomeVeiculo(x.u.Veiculo),
                x.u.Empresa?.Nome ?? "",
                x.a.Litros,
                x.a.ValorTotal,
                x.a.ValorPorLitro ?? (x.a.Litros > 0 ? Math.Round(x.a.ValorTotal / x.a.Litros, 3) : 0),
                x.a.Odometro,
                x.a.Latitude is { } lat && x.a.Longitude is { } lng
                    ? $"https://www.google.com/maps?q={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : "",
            });
        var ws = Tabela(wb, "Abastecimentos", cab, linhas);
        Formatar(ws, [1], FormatoDataHora);
        Formatar(ws, [5], FormatoLitros);
        Formatar(ws, [6], FormatoReais);
        Formatar(ws, [7], "\"R$\" #,##0.000");
        Formatar(ws, [8], FormatoKm);
        foreach (var cel in ws.Column(9).CellsUsed().Skip(1).Where(c => c.GetString().StartsWith("http")))
            cel.SetHyperlink(new XLHyperlink(cel.GetString()));
        Ajustar(ws);
    }

    private static void Consumo(XLWorkbook wb, IReadOnlyList<ConsumoMesDto> consumo)
    {
        string[] cab = ["Mês", "Veículo", "Km rodados", "Litros", "Gasto (R$)", "Km por litro", "R$ por km", "Preço médio do litro"];
        var linhas = consumo.Select(c => new XLCellValue[]
        {
            $"{c.Mes:00}/{c.Ano}",
            c.Veiculo,
            c.KmRodados,
            c.Litros,
            c.Gasto,
            c.KmPorLitro is { } kml ? kml : Blank.Value,
            c.CustoPorKm is { } rkm ? rkm : Blank.Value,
            c.PrecoMedioLitro is { } pm ? pm : Blank.Value,
        });
        var ws = Tabela(wb, "Consumo", cab, linhas);
        Formatar(ws, [3], FormatoKm);
        Formatar(ws, [4], FormatoLitros);
        Formatar(ws, [5, 7], FormatoReais);
        Formatar(ws, [6], "#,##0.0");
        Formatar(ws, [8], "\"R$\" #,##0.000");
        Ajustar(ws);
    }

    private static void Alertas(XLWorkbook wb, IReadOnlyList<AlertaDto> alertas)
    {
        string[] cab = ["Gravidade", "Tipo", "Quando", "Descrição"];
        var linhas = alertas.Select(a => new XLCellValue[]
        {
            a.Gravidade switch { GravidadeAlerta.Alta => "Alta", GravidadeAlerta.Media => "Média", _ => "Baixa" },
            a.Tipo,
            a.Quando is { } q ? Local(q) : Blank.Value,
            a.Descricao,
        });
        var ws = Tabela(wb, "Alertas", cab, linhas);
        Formatar(ws, [3], FormatoDataHora);
        Ajustar(ws);
        ws.Column(4).Width = Math.Min(ws.Column(4).Width, 110);
    }

    private static IXLWorksheet Tabela(XLWorkbook wb, string aba, string[] cabecalho, IEnumerable<XLCellValue[]> linhas)
    {
        var ws = wb.AddWorksheet(aba);
        for (var c = 0; c < cabecalho.Length; c++)
            ws.Cell(1, c + 1).Value = cabecalho[c];

        var r = 2;
        foreach (var linha in linhas)
        {
            for (var c = 0; c < linha.Length; c++)
                ws.Cell(r, c + 1).Value = linha[c];
            r++;
        }

        // Tabela precisa de ao menos uma linha de dados.
        if (r == 2)
            ws.Cell(2, 1).Value = "(nenhum registro no período)";
        var tabela = ws.Range(1, 1, Math.Max(r - 1, 2), cabecalho.Length).CreateTable();
        tabela.Theme = XLTableTheme.TableStyleMedium2;
        ws.SheetView.FreezeRows(1);
        return ws;
    }

    private static void Formatar(IXLWorksheet ws, int[] colunas, string formato)
    {
        foreach (var c in colunas)
            ws.Column(c).Style.NumberFormat.Format = formato;
    }

    private static void Ajustar(IXLWorksheet ws)
    {
        ws.Columns().AdjustToContents(1, 200);
        foreach (var col in ws.ColumnsUsed())
            col.Width = Math.Min(col.Width + 2, 60);
    }

    /// <summary>Excel não tem fuso: grava a data/hora já no horário de Brasília.</summary>
    private static DateTime Local(DateTimeOffset data) => data.ToOffset(RelatorioCalculadora.Brasilia).DateTime;
}
