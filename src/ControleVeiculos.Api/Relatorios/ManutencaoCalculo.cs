using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Domain.Enums;
using ControleVeiculos.Shared.Manutencoes;

namespace ControleVeiculos.Api.Relatorios;

/// <summary>Regras das próximas manutenções e do custo real de manutenção por km.</summary>
public static class ManutencaoCalculo
{
    /// <summary>Falta menos que isso (km ou dias) → "em breve".</summary>
    public const int AvisoKm = 1000;
    public const int AvisoDias = 30;

    /// <summary>Categorias que esta manutenção cobriu: as dos itens + as que têm "próxima" marcada.</summary>
    public static HashSet<string> Categorias(Manutencao m) =>
        m.Itens.Select(i => CategoriasManutencao.Classificar(i.Descricao))
            .Where(c => c is not null).Select(c => c!)
            .Concat(m.Proximas.Select(p => p.Tipo))
            .ToHashSet();

    /// <summary>Gasto sem os pneus (que têm custo por km próprio nos parâmetros).</summary>
    public static decimal GastoSemPneus(Manutencao m) =>
        m.Valor - m.Itens.Where(i => CategoriasManutencao.Classificar(i.Descricao) == CategoriasManutencao.Pneus).Sum(i => i.Valor);

    public static List<ProximaManutencaoDto> Proximas(IEnumerable<Manutencao> manutencoes, IEnumerable<Vehicle> veiculos, DateOnly hoje)
    {
        var porVeiculo = veiculos.ToDictionary(v => v.Id);
        return manutencoes
            .Where(m => porVeiculo.ContainsKey(m.VeiculoId))
            .SelectMany(m => Categorias(m).Select(c => (Manutencao: m, Categoria: c)))
            .GroupBy(x => (x.Manutencao.VeiculoId, x.Categoria))
            // Vale a mais recente de cada categoria: a troca de óleo nova "encerra" o aviso da anterior.
            .Select(g => g.OrderByDescending(x => x.Manutencao.Data).ThenByDescending(x => x.Manutencao.Km).First())
            .Select(x => (x.Manutencao, Proxima: x.Manutencao.Proximas.FirstOrDefault(p => p.Tipo == x.Categoria)))
            .Where(x => x.Proxima is not null && (x.Proxima.ProximaKm is not null || x.Proxima.ProximaData is not null))
            .Select(x =>
            {
                var v = porVeiculo[x.Manutencao.VeiculoId];
                var p = x.Proxima!;
                int? faltamKm = p.ProximaKm is { } pk ? pk - v.OdometroAtual : null;
                int? faltamDias = p.ProximaData is { } pd ? pd.DayNumber - hoje.DayNumber : null;
                var situacao = faltamKm <= 0 || faltamDias <= 0 ? SituacaoManutencao.Vencida
                    : faltamKm <= AvisoKm || faltamDias <= AvisoDias ? SituacaoManutencao.EmBreve
                    : SituacaoManutencao.EmDia;
                return new ProximaManutencaoDto(v.Id, RelatorioCalculadora.NomeVeiculo(v), p.Tipo, p.ProximaKm, p.ProximaData,
                    faltamKm, faltamDias, situacao, x.Manutencao.Id);
            })
            .OrderByDescending(p => p.Situacao)
            .ThenBy(p => p.FaltamKm ?? int.MaxValue)
            .ToList();
    }

    public static ManutencaoResumoDto Resumo(Vehicle veiculo, IEnumerable<Manutencao> manutencoes, IEnumerable<UsageRecord> usos, DateOnly hoje)
    {
        var desde = hoje.AddYears(-1);
        var doCarro = manutencoes.Where(m => m.VeiculoId == veiculo.Id).ToList();
        var noAno = doCarro.Where(m => m.Data > desde).ToList();
        var km = usos.Where(u => u.VeiculoId == veiculo.Id && u.Status == UsageRecordStatus.Finalizado
                && u.IniciadoEm >= RelatorioCalculadora.InicioDoDia(desde))
            .Sum(RelatorioCalculadora.Km);
        var semPneus = noAno.Sum(GastoSemPneus);
        return new ManutencaoResumoDto(
            veiculo.Id, RelatorioCalculadora.NomeVeiculo(veiculo), veiculo.OdometroAtual,
            noAno.Sum(m => m.Valor), km,
            km > 0 && semPneus > 0 ? Math.Round(semPneus / km, 4) : null,
            Proximas(doCarro, [veiculo], hoje));
    }
}
