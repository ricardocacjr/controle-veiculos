using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Domain.Enums;
using ControleVeiculos.Shared.Relatorios;

namespace ControleVeiculos.Api.Relatorios;

/// <summary>Seção "Base" do appsettings: de onde os veículos saem (mesma configuração do Web).</summary>
public class BaseOperacional
{
    public string Nome { get; set; } = "Base";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RaioMetros { get; set; } = 300;

    public bool Configurada => Latitude != 0 || Longitude != 0;

    public double DistanciaMetros(double latitude, double longitude)
    {
        const double raioTerra = 6_371_000;
        var dLat = Rad(latitude - Latitude);
        var dLon = Rad(longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(Latitude)) * Math.Cos(Rad(latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return raioTerra * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Rad(double graus) => graus * Math.PI / 180;
}

/// <summary>
/// Monta o relatório gerencial a partir das saídas. Datas e meses são sempre no horário de
/// Brasília (o servidor roda em UTC). Km e horas contam só saídas encerradas.
/// </summary>
public static class RelatorioCalculadora
{
    public static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    /// <summary>A Api roda em cultura invariante (coordenadas); textos pro gestor saem em pt-BR ("80.190 km").</summary>
    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    /// <summary>Saída sem chegada há mais que isso vira alerta.</summary>
    public const int HorasSaidaAberta = 12;
    /// <summary>Diferença tolerada entre a chegada de uma saída e a saída seguinte do mesmo carro.</summary>
    public const int ToleranciaKmEntreSaidas = 20;
    /// <summary>Uma única saída acima disso é conferida.</summary>
    public const int KmSaidaAlta = 500;
    /// <summary>Meses mostrados no consumo (terminando no mês do fim do período).</summary>
    public const int MesesConsumo = 12;

    public static DateTimeOffset InicioDoDia(DateOnly dia) => new(dia.ToDateTime(TimeOnly.MinValue), Brasilia);

    public static DateOnly Hoje(DateTimeOffset agora) => DateOnly.FromDateTime(agora.ToOffset(Brasilia).DateTime);

    /// <summary>Quanto histórico carregar: o período, os 12 meses do consumo e um mês antes (pra comparar o km com a saída anterior).</summary>
    public static DateTimeOffset CarregarDesde(DateOnly de, DateOnly ate)
    {
        var inicioConsumo = new DateOnly(ate.Year, ate.Month, 1).AddMonths(-(MesesConsumo - 1));
        return InicioDoDia((de < inicioConsumo ? de : inicioConsumo).AddMonths(-1));
    }

    public static bool NoPeriodo(UsageRecord u, DateOnly de, DateOnly ate) =>
        u.Status != UsageRecordStatus.Cancelado && u.IniciadoEm >= InicioDoDia(de) && u.IniciadoEm < InicioDoDia(ate.AddDays(1));

    public static int Km(UsageRecord u) =>
        u.Status == UsageRecordStatus.Finalizado && u.OdometroFinal is { } fim ? Math.Max(0, fim - u.OdometroInicial) : 0;

    public static double Horas(UsageRecord u) =>
        u.Status == UsageRecordStatus.Finalizado && u.FinalizadoEm is { } fim ? Math.Max(0, (fim - u.IniciadoEm).TotalHours) : 0;

    public static decimal Gasto(UsageRecord u) => u.Abastecimentos.Sum(a => a.ValorTotal);

    public static decimal Litros(UsageRecord u) => u.Abastecimentos.Sum(a => a.Litros);

    public static string NomeVeiculo(Vehicle? v) => v is null ? "?" : $"{v.Placa} · {v.Modelo}";

    public static RelatorioDto Calcular(IReadOnlyList<UsageRecord> carregadas, DateOnly de, DateOnly ate, BaseOperacional? baseOperacional, DateTimeOffset agora)
    {
        var validas = carregadas.Where(u => u.Status != UsageRecordStatus.Cancelado).OrderBy(u => u.IniciadoEm).ToList();
        var doPeriodo = validas.Where(u => NoPeriodo(u, de, ate)).ToList();

        var resumo = new ResumoDto(
            doPeriodo.Count,
            doPeriodo.Count(u => u.Status == UsageRecordStatus.EmAndamento),
            doPeriodo.Sum(Km),
            Math.Round(doPeriodo.Sum(Horas), 1),
            doPeriodo.Sum(u => u.Abastecimentos.Count),
            doPeriodo.Sum(Litros),
            doPeriodo.Sum(Gasto));

        return new RelatorioDto(
            de, ate, resumo,
            Agrupar(doPeriodo, u => u.Motorista?.Nome),
            Agrupar(doPeriodo, u => u.Empresa?.Nome),
            Agrupar(doPeriodo, u => u.Finalidade),
            Consumo(validas, ate),
            Alertas(validas, doPeriodo, baseOperacional, agora));
    }

    private static List<GrupoDto> Agrupar(IEnumerable<UsageRecord> usos, Func<UsageRecord, string?> chave) =>
        usos.GroupBy(u => (chave(u) ?? "").Trim().ToUpperInvariant())
            .Select(g => new GrupoDto(
                string.IsNullOrWhiteSpace(g.Key) ? "(sem informação)" : (chave(g.First()) ?? "").Trim(),
                g.Count(),
                g.Sum(Km),
                Math.Round(g.Sum(Horas), 1),
                g.Sum(Gasto)))
            .OrderByDescending(g => g.KmRodados)
            .ThenByDescending(g => g.Saidas)
            .ToList();

    private static List<ConsumoMesDto> Consumo(IReadOnlyList<UsageRecord> validas, DateOnly ate)
    {
        var fimMes = new DateOnly(ate.Year, ate.Month, 1);
        var inicioMes = fimMes.AddMonths(-(MesesConsumo - 1));
        static DateOnly MesDe(DateTimeOffset data)
        {
            var local = data.ToOffset(Brasilia);
            return new DateOnly(local.Year, local.Month, 1);
        }

        // Km pelo mês da saída; litros/valor pelo mês do abastecimento.
        var km = validas
            .GroupBy(u => (Veiculo: NomeVeiculo(u.Veiculo), Mes: MesDe(u.IniciadoEm)))
            .ToDictionary(g => g.Key, g => g.Sum(Km));
        var combustivel = validas
            .SelectMany(u => u.Abastecimentos.Select(a => (Veiculo: NomeVeiculo(u.Veiculo), a)))
            .GroupBy(x => (x.Veiculo, Mes: MesDe(x.a.CreatedAt)))
            .ToDictionary(g => g.Key, g => (Litros: g.Sum(x => x.a.Litros), Valor: g.Sum(x => x.a.ValorTotal)));

        return km.Keys.Union(combustivel.Keys)
            .Where(k => k.Mes >= inicioMes && k.Mes <= fimMes)
            .Select(k =>
            {
                var kmMes = km.GetValueOrDefault(k);
                var (litros, valor) = combustivel.GetValueOrDefault(k);
                return new ConsumoMesDto(
                    k.Mes.Year, k.Mes.Month, k.Veiculo, kmMes, litros, valor,
                    litros > 0 && kmMes > 0 ? Math.Round(kmMes / litros, 1) : null,
                    kmMes > 0 && valor > 0 ? Math.Round(valor / kmMes, 2) : null,
                    litros > 0 ? Math.Round(valor / litros, 3) : null);
            })
            .Where(c => c.KmRodados > 0 || c.Litros > 0)
            .OrderByDescending(c => c.Ano).ThenByDescending(c => c.Mes).ThenBy(c => c.Veiculo)
            .ToList();
    }

    private static List<AlertaDto> Alertas(IReadOnlyList<UsageRecord> validas, IReadOnlyList<UsageRecord> doPeriodo, BaseOperacional? baseOperacional, DateTimeOffset agora)
    {
        var alertas = new List<AlertaDto>();
        var noPeriodo = doPeriodo.Select(u => u.Id).ToHashSet();
        static string Quem(UsageRecord u) => u.Motorista?.Nome ?? "?";
        static string Data(DateTimeOffset d) => d.ToOffset(Brasilia).ToString("dd/MM HH:mm");

        // 1. Saídas abertas há muito tempo — sempre, independente do período escolhido.
        foreach (var u in validas.Where(u => u.Status == UsageRecordStatus.EmAndamento))
        {
            var horas = (agora - u.IniciadoEm).TotalHours;
            if (horas > HorasSaidaAberta)
                alertas.Add(new("Saída sem chegada", GravidadeAlerta.Alta,
                    string.Create(PtBr, $"{Quem(u)} saiu com {u.Veiculo?.Placa} em {Data(u.IniciadoEm)} e não registrou a chegada (há {horas:N0} h)."),
                    u.Id, u.IniciadoEm));
        }

        // 2. Km que "sumiu" ou voltou entre uma chegada e a saída seguinte do mesmo carro.
        foreach (var carro in validas.GroupBy(u => u.VeiculoId))
        {
            UsageRecord? anterior = null;
            foreach (var u in carro.OrderBy(u => u.IniciadoEm))
            {
                if (anterior?.OdometroFinal is { } chegada && noPeriodo.Contains(u.Id))
                {
                    var diferenca = u.OdometroInicial - chegada;
                    if (diferenca > ToleranciaKmEntreSaidas)
                        alertas.Add(new("Km sem registro", GravidadeAlerta.Alta,
                            string.Create(PtBr, $"{diferenca:N0} km rodados sem registro no {u.Veiculo?.Placa}: chegou com {chegada:N0} km ({Quem(anterior)}, {Data(anterior.FinalizadoEm ?? anterior.IniciadoEm)}) e saiu com {u.OdometroInicial:N0} km ({Quem(u)}, {Data(u.IniciadoEm)})."),
                            u.Id, u.IniciadoEm));
                    else if (diferenca < -ToleranciaKmEntreSaidas)
                        alertas.Add(new("Km menor que a última chegada", GravidadeAlerta.Media,
                            string.Create(PtBr, $"{Quem(u)} saiu com {u.OdometroInicial:N0} km, mas o {u.Veiculo?.Placa} tinha chegado com {chegada:N0} km. Confira a foto do painel."),
                            u.Id, u.IniciadoEm));
                }
                if (u.Status == UsageRecordStatus.Finalizado)
                    anterior = u;
            }
        }

        foreach (var u in doPeriodo)
        {
            // 3. Saída com km muito alto.
            if (Km(u) > KmSaidaAlta)
                alertas.Add(new("Km alto", GravidadeAlerta.Media,
                    string.Create(PtBr, $"{Quem(u)} rodou {Km(u):N0} km numa só saída ({Data(u.IniciadoEm)}, {u.Finalidade}). Confira se o km está certo."),
                    u.Id, u.IniciadoEm));

            // 4. Onde começou a saída.
            if (u.LatitudeInicial is { } lat && u.LongitudeInicial is { } lng)
            {
                if (baseOperacional is { Configurada: true } b && b.DistanciaMetros(lat, lng) is var metros && metros > b.RaioMetros)
                    alertas.Add(new("Saída fora da base", GravidadeAlerta.Media,
                        string.Create(PtBr, $"{Quem(u)} iniciou a saída a {metros / 1000:N1} km da base ({u.Origem ?? "local marcado no mapa"}), em {Data(u.IniciadoEm)}."),
                        u.Id, u.IniciadoEm));
            }
            else
            {
                alertas.Add(new("Saída sem localização", GravidadeAlerta.Baixa,
                    string.Create(PtBr, $"A saída de {Quem(u)} em {Data(u.IniciadoEm)} foi registrada sem GPS (não dá pra saber se começou na base)."),
                    u.Id, u.IniciadoEm));
            }

            // 5. Abastecimento sem foto do comprovante.
            var comprovantes = u.Fotos.Count(f => f.Tipo == VehiclePhotoType.ComprovanteAbastecimento);
            if (u.Abastecimentos.Count > comprovantes)
                alertas.Add(new("Abastecimento sem comprovante", GravidadeAlerta.Media,
                    string.Create(PtBr, $"{Quem(u)} registrou {u.Abastecimentos.Count} abastecimento(s) em {Data(u.IniciadoEm)}, mas só {comprovantes} foto(s) de comprovante."),
                    u.Id, u.IniciadoEm));
        }

        return alertas
            .OrderByDescending(a => a.Gravidade)
            .ThenByDescending(a => a.Quando)
            .ToList();
    }
}
