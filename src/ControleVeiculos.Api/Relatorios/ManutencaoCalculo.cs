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

    /// <summary>Pneus têm custo por km próprio (parâmetros) — ficam fora da "manutenção por km" real.</summary>
    public const string TipoPneus = "PNEUS";

    public static List<ProximaManutencaoDto> Proximas(IEnumerable<Manutencao> manutencoes, IEnumerable<Vehicle> veiculos, DateOnly hoje)
    {
        var porVeiculo = veiculos.ToDictionary(v => v.Id);
        return manutencoes
            .GroupBy(m => (m.VeiculoId, m.Tipo))
            // Vale a mais recente de cada tipo: a troca de óleo nova "encerra" o aviso da anterior.
            .Select(g => g.OrderByDescending(m => m.Data).ThenByDescending(m => m.Km).First())
            .Where(m => m.ProximaKm is not null || m.ProximaData is not null)
            .Where(m => porVeiculo.ContainsKey(m.VeiculoId))
            .Select(m =>
            {
                var v = porVeiculo[m.VeiculoId];
                int? faltamKm = m.ProximaKm is { } pk ? pk - v.OdometroAtual : null;
                int? faltamDias = m.ProximaData is { } pd ? pd.DayNumber - hoje.DayNumber : null;
                var situacao = faltamKm <= 0 || faltamDias <= 0 ? SituacaoManutencao.Vencida
                    : faltamKm <= AvisoKm || faltamDias <= AvisoDias ? SituacaoManutencao.EmBreve
                    : SituacaoManutencao.EmDia;
                return new ProximaManutencaoDto(v.Id, RelatorioCalculadora.NomeVeiculo(v), m.Tipo, m.ProximaKm, m.ProximaData,
                    faltamKm, faltamDias, situacao, m.Id);
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
        var semPneus = noAno.Where(m => m.Tipo != TipoPneus).Sum(m => m.Valor);
        return new ManutencaoResumoDto(
            veiculo.Id, RelatorioCalculadora.NomeVeiculo(veiculo), veiculo.OdometroAtual,
            noAno.Sum(m => m.Valor), km,
            km > 0 && semPneus > 0 ? Math.Round(semPneus / km, 4) : null,
            Proximas(doCarro, [veiculo], hoje));
    }
}
