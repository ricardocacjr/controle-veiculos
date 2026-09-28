using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>
/// Quanto custa o km além do combustível (pneus + manutenção) e a tarifa fixa usada pra cobrar
/// cada categoria (empresa ou uso pessoal). Um registro só; sem registro valem os padrões abaixo
/// (os mesmos da planilha que a empresa usava antes do app).
/// </summary>
public class ParametrosCusto : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    /// <summary>Valor cobrado por km rodado (fixo; revisado quando os custos mudam).</summary>
    public decimal TarifaPorKm { get; set; } = 0.8642m;

    public decimal PrecoJogoPneus { get; set; } = 1600m;
    public int VidaUtilPneusKm { get; set; } = 40000;

    /// <summary>Óleo, filtros, freios, revisões — R$ por km.</summary>
    public decimal ManutencaoPorKm { get; set; } = 0.15m;

    public decimal PneusPorKm => VidaUtilPneusKm > 0 ? PrecoJogoPneus / VidaUtilPneusKm : 0;
    public decimal ExtraPorKm => PneusPorKm + ManutencaoPorKm;
}
