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

/// <summary>Um ciclo de tanque cheio → tanque cheio, com o abastecimento que o fechou.</summary>
public record Ciclo(Guid VeiculoId, string Veiculo, FuelEntry Abertura, FuelEntry Fechamento, UsageRecord UsoFechamento,
    int Km, int Abastecimentos, decimal Litros, decimal Gasto)
{
    public decimal? KmPorLitro => Litros > 0 && Km > 0 ? Math.Round(Km / Litros, 2) : null;
    public decimal? CustoPorKm => Km > 0 ? Math.Round(Gasto / Km, 4) : null;

    /// <summary>Consumo possível pra um carro (2 a 40 km/L). Fora disso é km ou litros errados (ou abastecimentos
    /// de testes misturados): não entra no custo nem na referência de "normal" — só vira alerta.</summary>
    public bool Plausivel => KmPorLitro is >= 2 and <= 40;
}

/// <summary>
/// Monta o relatório gerencial a partir das saídas. Datas e meses são sempre no horário de
/// Brasília (o servidor roda em UTC). Km e horas contam só saídas encerradas.
///
/// Custos seguem a planilha que a empresa usava: o combustível do km sai dos ciclos de tanque
/// cheio (o primeiro tanque cheio é o "dia zero" — só marca o ponto de partida), o extra
/// (pneus + manutenção) sai dos parâmetros, e a cobrança usa uma tarifa fixa por km.
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
    /// <summary>Sem ciclo fechado no período, o custo do combustível usa os últimos ciclos.</summary>
    public const int CiclosRecentes = 3;

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

    private static bool NoPeriodo(DateTimeOffset quando, DateOnly de, DateOnly ate) =>
        quando >= InicioDoDia(de) && quando < InicioDoDia(ate.AddDays(1));

    public static int Km(UsageRecord u) =>
        u.Status == UsageRecordStatus.Finalizado && u.OdometroFinal is { } fim ? Math.Max(0, fim - u.OdometroInicial) : 0;

    public static double Horas(UsageRecord u) =>
        u.Status == UsageRecordStatus.Finalizado && u.FinalizadoEm is { } fim ? Math.Max(0, (fim - u.IniciadoEm).TotalHours) : 0;

    /// <summary>Combustível da saída, sem os abastecimentos do "dia zero" (primeiro tanque cheio do carro: repôs km de antes do sistema).</summary>
    public static decimal Gasto(UsageRecord u, IReadOnlySet<Guid>? diaZero = null) => Contam(u, diaZero).Sum(a => a.ValorTotal);

    public static decimal PagoPeloMotorista(UsageRecord u, IReadOnlySet<Guid>? diaZero = null) => Contam(u, diaZero).Where(a => a.PagoPeloMotorista).Sum(a => a.ValorTotal);

    public static decimal Litros(UsageRecord u, IReadOnlySet<Guid>? diaZero = null) => Contam(u, diaZero).Sum(a => a.Litros);

    private static IEnumerable<FuelEntry> Contam(UsageRecord u, IReadOnlySet<Guid>? diaZero) =>
        diaZero is null ? u.Abastecimentos : u.Abastecimentos.Where(a => !diaZero.Contains(a.Id));

    /// <summary>km × tarifa. Saída em que o motorista abasteceu do próprio bolso fica quitada (zero) — regra da planilha da empresa.</summary>
    public static decimal ValorACobrar(UsageRecord u, decimal tarifa) =>
        u.Abastecimentos.Any(a => a.PagoPeloMotorista) ? 0 : Math.Round(Km(u) * tarifa, 2);

    public static string NomeVeiculo(Vehicle? v) => v is null ? "?" : $"{v.Placa} · {v.Modelo}";

    /// <summary>
    /// Ciclos de tanque cheio de cada carro, na ordem do km. O abastecimento que fecha o ciclo (e os
    /// parciais no meio) repõe o que foi gasto desde o tanque cheio anterior.
    /// </summary>
    public static List<Ciclo> Ciclos(IReadOnlyList<UsageRecord> validas)
    {
        var ciclos = new List<Ciclo>();
        foreach (var carro in validas.GroupBy(u => u.VeiculoId))
        {
            var entradas = carro
                .SelectMany(u => u.Abastecimentos.Select(a => (Uso: u, Abast: a)))
                .OrderBy(x => x.Abast.Odometro).ThenBy(x => x.Abast.CreatedAt)
                .ToList();

            FuelEntry? abertura = null;
            var acumulado = new List<FuelEntry>();
            foreach (var (uso, a) in entradas)
            {
                if (abertura is null)
                {
                    // Dia zero: só o primeiro tanque cheio conhecido abre a contagem.
                    if (a.TanqueCheio)
                        abertura = a;
                    continue;
                }

                acumulado.Add(a);
                if (!a.TanqueCheio)
                    continue;

                ciclos.Add(new Ciclo(carro.Key, NomeVeiculo(uso.Veiculo), abertura, a, uso,
                    Math.Max(0, a.Odometro - abertura.Odometro), acumulado.Count,
                    acumulado.Sum(x => x.Litros), acumulado.Sum(x => x.ValorTotal)));
                abertura = a;
                acumulado = [];
            }
        }
        return ciclos.OrderBy(c => c.Fechamento.CreatedAt).ToList();
    }

    /// <summary>Custo do combustível por km: ciclos que fecharam no período; sem nenhum, os últimos ciclos até o fim do período.</summary>
    public static (decimal? PorKm, string? Origem, decimal? KmPorLitro) CombustivelPorKm(IReadOnlyList<Ciclo> ciclos, DateOnly de, DateOnly ate)
    {
        var validos = ciclos.Where(c => c.Plausivel).ToList();
        var base_ = validos.Where(c => NoPeriodo(c.Fechamento.CreatedAt, de, ate)).ToList();
        var origem = "ciclos de tanque cheio do período";
        if (base_.Count == 0)
        {
            base_ = validos.Where(c => c.Fechamento.CreatedAt < InicioDoDia(ate.AddDays(1))).TakeLast(CiclosRecentes).ToList();
            origem = "últimos ciclos de tanque cheio";
        }
        if (base_.Count == 0)
            return (null, null, null);

        var km = base_.Sum(c => c.Km);
        return (Math.Round(base_.Sum(c => c.Gasto) / km, 4), origem, Math.Round(km / base_.Sum(c => c.Litros), 2));
    }

    public static RelatorioDto Calcular(IReadOnlyList<UsageRecord> carregadas, DateOnly de, DateOnly ate,
        BaseOperacional? baseOperacional, ParametrosCusto parametros, DateTimeOffset agora, IReadOnlySet<Guid>? diaZero = null)
    {
        var validas = carregadas.Where(u => u.Status != UsageRecordStatus.Cancelado).OrderBy(u => u.IniciadoEm).ToList();
        var doPeriodo = validas.Where(u => NoPeriodo(u, de, ate)).ToList();
        var ciclos = Ciclos(validas);

        var gasto = doPeriodo.Sum(u => Gasto(u, diaZero));
        var pago = doPeriodo.Sum(u => PagoPeloMotorista(u, diaZero));
        var foraDaConta = doPeriodo.SelectMany(u => u.Abastecimentos).Where(a => diaZero?.Contains(a.Id) == true).Sum(a => a.ValorTotal);
        var resumo = new ResumoDto(
            doPeriodo.Count,
            doPeriodo.Count(u => u.Status == UsageRecordStatus.EmAndamento),
            doPeriodo.Sum(Km),
            Math.Round(doPeriodo.Sum(Horas), 1),
            doPeriodo.Sum(u => u.Abastecimentos.Count(a => diaZero?.Contains(a.Id) != true)),
            doPeriodo.Sum(u => Litros(u, diaZero)),
            gasto,
            gasto - pago,
            pago,
            foraDaConta);

        var (combustivelKm, origem, kmPorLitro) = CombustivelPorKm(ciclos, de, ate);
        var custoKm = combustivelKm is { } c ? c + parametros.ExtraPorKm : (decimal?)null;
        var custoKmEfetivo = custoKm ?? parametros.ExtraPorKm;
        var litrosPeriodo = doPeriodo.Sum(u => Litros(u, diaZero));
        var custos = new CustosDto(
            combustivelKm, origem,
            Math.Round(parametros.PneusPorKm, 4), parametros.ManutencaoPorKm, Math.Round(parametros.ExtraPorKm, 4),
            custoKm is { } ck ? Math.Round(ck, 4) : null,
            parametros.TarifaPorKm,
            Math.Round(resumo.KmRodados * custoKmEfetivo, 2),
            doPeriodo.Sum(u => ValorACobrar(u, parametros.TarifaPorKm)),
            kmPorLitro,
            litrosPeriodo > 0 ? Math.Round(gasto / litrosPeriodo, 3) : null,
            combustivelKm is { } ckm ? Math.Round(resumo.KmRodados * ckm, 2) : null);

        return new RelatorioDto(
            de, ate, resumo, custos,
            Agrupar(doPeriodo, u => u.Motorista?.Nome, custoKmEfetivo, parametros.TarifaPorKm, diaZero),
            Agrupar(doPeriodo, u => u.Empresa?.Nome, custoKmEfetivo, parametros.TarifaPorKm, diaZero),
            Agrupar(doPeriodo, u => u.Finalidade, custoKmEfetivo, parametros.TarifaPorKm, diaZero),
            Consumo(validas, ciclos, ate),
            ciclos.Where(ci => NoPeriodo(ci.Fechamento.CreatedAt, de, ate))
                .OrderByDescending(ci => ci.Fechamento.CreatedAt)
                .Select(ci => new CicloDto(ci.Veiculo, ci.Abertura.CreatedAt, ci.Fechamento.CreatedAt,
                    ci.Abertura.Odometro, ci.Fechamento.Odometro, ci.Km, ci.Abastecimentos, ci.Litros, ci.Gasto,
                    ci.KmPorLitro, ci.CustoPorKm is { } cpk ? Math.Round(cpk, 4) : null))
                .ToList(),
            Alertas(validas, doPeriodo, ciclos, de, ate, baseOperacional, agora));
    }

    private static List<GrupoDto> Agrupar(IReadOnlyList<UsageRecord> usos, Func<UsageRecord, string?> chave, decimal custoKm, decimal tarifa, IReadOnlySet<Guid>? diaZero)
    {
        var kmTotal = Math.Max(1, usos.Sum(Km));
        return usos.GroupBy(u => (chave(u) ?? "").Trim().ToUpperInvariant())
            .Select(g =>
            {
                var km = g.Sum(Km);
                return new GrupoDto(
                    string.IsNullOrWhiteSpace(g.Key) ? "(sem informação)" : (chave(g.First()) ?? "").Trim(),
                    g.Count(),
                    km,
                    Math.Round(g.Sum(Horas), 1),
                    g.Sum(u => Gasto(u, diaZero)),
                    Math.Round(100.0 * km / kmTotal, 1),
                    Math.Round(km * custoKm, 2),
                    g.Sum(u => PagoPeloMotorista(u, diaZero)),
                    g.Sum(u => ValorACobrar(u, tarifa)));
            })
            .OrderByDescending(g => g.KmRodados)
            .ThenByDescending(g => g.Saidas)
            .ToList();
    }

    private static List<ConsumoMesDto> Consumo(IReadOnlyList<UsageRecord> validas, IReadOnlyList<Ciclo> ciclos, DateOnly ate)
    {
        var fimMes = new DateOnly(ate.Year, ate.Month, 1);
        var inicioMes = fimMes.AddMonths(-(MesesConsumo - 1));
        static DateOnly MesDe(DateTimeOffset data)
        {
            var local = data.ToOffset(Brasilia);
            return new DateOnly(local.Year, local.Month, 1);
        }

        // Km pelo mês da saída; litros/valor pelo mês do abastecimento; km/L e R$/km pelos ciclos que fecharam no mês.
        var km = validas
            .GroupBy(u => (Veiculo: NomeVeiculo(u.Veiculo), Mes: MesDe(u.IniciadoEm)))
            .ToDictionary(g => g.Key, g => g.Sum(Km));
        var combustivel = validas
            .SelectMany(u => u.Abastecimentos.Select(a => (Veiculo: NomeVeiculo(u.Veiculo), a)))
            .GroupBy(x => (x.Veiculo, Mes: MesDe(x.a.CreatedAt)))
            .ToDictionary(g => g.Key, g => (Litros: g.Sum(x => x.a.Litros), Valor: g.Sum(x => x.a.ValorTotal)));
        var porCiclo = ciclos
            .Where(c => c.Plausivel)
            .GroupBy(c => (c.Veiculo, Mes: MesDe(c.Fechamento.CreatedAt)))
            .ToDictionary(g => g.Key, g => (Km: g.Sum(c => c.Km), Litros: g.Sum(c => c.Litros), Gasto: g.Sum(c => c.Gasto)));

        return km.Keys.Union(combustivel.Keys)
            .Where(k => k.Mes >= inicioMes && k.Mes <= fimMes)
            .Select(k =>
            {
                var kmMes = km.GetValueOrDefault(k);
                var (litros, valor) = combustivel.GetValueOrDefault(k);
                var temCiclo = porCiclo.TryGetValue(k, out var ci);
                return new ConsumoMesDto(
                    k.Mes.Year, k.Mes.Month, k.Veiculo, kmMes, litros, valor,
                    temCiclo ? Math.Round(ci.Km / ci.Litros, 1) : null,
                    temCiclo ? Math.Round(ci.Gasto / ci.Km, 4) : null,
                    litros > 0 ? Math.Round(valor / litros, 3) : null);
            })
            .Where(c => c.KmRodados > 0 || c.Litros > 0)
            .OrderByDescending(c => c.Ano).ThenByDescending(c => c.Mes).ThenBy(c => c.Veiculo)
            .ToList();
    }

    private static decimal Mediana(IReadOnlyList<decimal> valores)
    {
        var ordenados = valores.Order().ToList();
        var meio = ordenados.Count / 2;
        return ordenados.Count % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2;
    }

    private static List<AlertaDto> Alertas(IReadOnlyList<UsageRecord> validas, IReadOnlyList<UsageRecord> doPeriodo, IReadOnlyList<Ciclo> ciclos,
        DateOnly de, DateOnly ate, BaseOperacional? baseOperacional, DateTimeOffset agora)
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

        // Km habitual de cada motorista em cada motivo (ex.: pernoite do Daniel ~8–15 km).
        var habitual = validas
            .Where(u => u.Status == UsageRecordStatus.Finalizado)
            .GroupBy(u => (u.MotoristaId, Motivo: u.Finalidade.Trim().ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var u in doPeriodo)
        {
            // 3. Saída com km muito alto, ou bem acima do que essa pessoa costuma rodar nesse motivo.
            var km = Km(u);
            if (km > KmSaidaAlta)
            {
                alertas.Add(new("Km alto", GravidadeAlerta.Media,
                    string.Create(PtBr, $"{Quem(u)} rodou {km:N0} km numa só saída ({Data(u.IniciadoEm)}, {u.Finalidade}). Confira se o km está certo."),
                    u.Id, u.IniciadoEm));
            }
            else if (km > 0 && habitual.TryGetValue((u.MotoristaId, u.Finalidade.Trim().ToUpperInvariant()), out var mesmas))
            {
                var outras = mesmas.Where(o => o.Id != u.Id).Select(o => (decimal)Km(o)).ToList();
                if (outras.Count >= 3)
                {
                    var normal = Mediana(outras);
                    if (km >= Math.Max(normal * 2.5m, normal + 10))
                        alertas.Add(new("Km acima do habitual", GravidadeAlerta.Media,
                            string.Create(PtBr, $"{Quem(u)} rodou {km:N0} km em \"{u.Finalidade}\" ({Data(u.IniciadoEm)}); o normal dele(a) nesse motivo é ~{normal:N0} km."),
                            u.Id, u.IniciadoEm));
                }
            }

            // 4. Onde começou a saída (saída importada do papel não tem GPS nem fotos — itens 4 a 6 não se aplicam).
            if (u.LatitudeInicial is { } lat && u.LongitudeInicial is { } lng)
            {
                if (baseOperacional is { Configurada: true } b && b.DistanciaMetros(lat, lng) is var metros && metros > b.RaioMetros)
                    alertas.Add(new("Saída fora da base", GravidadeAlerta.Media,
                        string.Create(PtBr, $"{Quem(u)} iniciou a saída a {metros / 1000:N1} km da base ({u.Origem ?? "local marcado no mapa"}), em {Data(u.IniciadoEm)}."),
                        u.Id, u.IniciadoEm));
            }
            else if (!u.Importado)
            {
                alertas.Add(new("Saída sem localização", GravidadeAlerta.Baixa,
                    string.Create(PtBr, $"A saída de {Quem(u)} em {Data(u.IniciadoEm)} foi registrada sem GPS (não dá pra saber se começou na base)."),
                    u.Id, u.IniciadoEm));
            }

            // 4b. Onde o carro ficou na chegada.
            if (u.Status == UsageRecordStatus.Finalizado && u.LatitudeFinal is { } latF && u.LongitudeFinal is { } lngF
                && baseOperacional is { Configurada: true } bc && bc.DistanciaMetros(latF, lngF) is var metrosF && metrosF > bc.RaioMetros)
                alertas.Add(new("Chegada fora da base", GravidadeAlerta.Media,
                    string.Create(PtBr, $"{Quem(u)} deixou o {u.Veiculo?.Placa} a {metrosF / 1000:N1} km da base ({u.Destino ?? "local marcado no mapa"}), em {Data(u.FinalizadoEm ?? u.IniciadoEm)}."),
                    u.Id, u.FinalizadoEm ?? u.IniciadoEm));

            // 5. Abastecimento sem foto do comprovante.
            var comprovantes = u.Fotos.Count(f => f.Tipo == VehiclePhotoType.ComprovanteAbastecimento);
            if (!u.Importado && u.Abastecimentos.Count > comprovantes)
                alertas.Add(new("Abastecimento sem comprovante", GravidadeAlerta.Media,
                    string.Create(PtBr, $"{Quem(u)} registrou {u.Abastecimentos.Count} abastecimento(s) em {Data(u.IniciadoEm)}, mas só {comprovantes} foto(s) de comprovante."),
                    u.Id, u.IniciadoEm));

            // 6. Chegada com km digitado (motorista sem câmera).
            if (!u.Importado && u.Status == UsageRecordStatus.Finalizado && !u.Fotos.Any(f => f.Tipo == VehiclePhotoType.OdometroFinal))
                alertas.Add(new("Chegada sem foto do painel", GravidadeAlerta.Media,
                    string.Create(PtBr, $"{Quem(u)} finalizou a saída de {Data(u.IniciadoEm)} digitando o km ({u.OdometroFinal:N0} km), sem foto do painel. Confira no carro."),
                    u.Id, u.FinalizadoEm ?? u.IniciadoEm));

            // 7. Combustível pago do bolso — a saída fica quitada (não entra na cobrança).
            foreach (var a in u.Abastecimentos.Where(a => a.PagoPeloMotorista))
                alertas.Add(new("Abastecimento pago pelo motorista", GravidadeAlerta.Baixa,
                    string.Create(PtBr, $"{Quem(u)} pagou R$ {a.ValorTotal:N2} ({a.Litros:N3} L) do próprio bolso em {Data(a.CreatedAt)}{(a.TanqueCheio ? "" : ", sem encher o tanque")}. Saída quitada: não entra na cobrança."),
                    u.Id, a.CreatedAt));
        }

        // 8. Ciclo com consumo fora do normal — quase sempre é tanque que não foi enchido de verdade.
        var comConsumo = ciclos.Where(c => c.KmPorLitro is not null).ToList();
        foreach (var c in comConsumo.Where(c => NoPeriodo(c.Fechamento.CreatedAt, de, ate)))
        {
            // Referência = km ÷ litros somados dos outros ciclos do carro: um tanque mal completado
            // puxa um ciclo pra cima e o seguinte pra baixo, e na soma isso se compensa.
            var outros = comConsumo.Where(o => o != c && o.VeiculoId == c.VeiculoId && o.Plausivel).ToList();
            var kml = c.KmPorLitro!.Value;
            if (!c.Plausivel)
            {
                alertas.Add(new("Km ou litros inconsistentes", GravidadeAlerta.Media,
                    string.Create(PtBr, $"Entre os abastecimentos de {Data(c.Abertura.CreatedAt)} ({c.Abertura.Odometro:N0} km) e {Data(c.Fechamento.CreatedAt)} ({c.Fechamento.Odometro:N0} km) o {c.Veiculo} daria {kml:N1} km/L, o que é impossível. Confira o km e os litros desses abastecimentos (ficaram fora do cálculo de custo)."),
                    c.UsoFechamento.Id, c.Fechamento.CreatedAt));
                continue;
            }

            var normal = outros.Count >= 2 ? outros.Sum(o => o.Km) / outros.Sum(o => o.Litros) : (decimal?)null;
            var fora = normal is { } n ? Math.Abs(kml - n) / n > 0.35m : kml is < 3 or > 25;
            if (!fora)
                continue;

            // Km/L alto = poucos litros no fechamento (quem fechou não completou o tanque);
            // km/L baixo = o tanque não estava cheio na abertura (quem abriu não completou).
            var alto = normal is { } ref_ ? kml > ref_ : kml > 25;
            var suspeito = alto ? c.Fechamento : c.Abertura;
            var usoSuspeito = alto ? c.UsoFechamento : validas.FirstOrDefault(u => u.Abastecimentos.Contains(c.Abertura));
            var quem = usoSuspeito is null ? "" : $" ({Quem(usoSuspeito)})";
            alertas.Add(new("Consumo fora do normal", GravidadeAlerta.Media,
                string.Create(PtBr, $"De {Data(c.Abertura.CreatedAt)} a {Data(c.Fechamento.CreatedAt)} o {c.Veiculo} fez {kml:N1} km/L{(normal is { } nn ? " (o normal é ~" + nn.ToString("N1", PtBr) + ")" : "")}. Provável tanque não completado no abastecimento de {Data(suspeito.CreatedAt)}{quem}, ou litros/km lidos errado."),
                (usoSuspeito ?? c.UsoFechamento).Id, suspeito.CreatedAt));
        }

        return alertas
            .OrderByDescending(a => a.Gravidade)
            .ThenByDescending(a => a.Quando)
            .ToList();
    }
}
