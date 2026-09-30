using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>
/// Manutenção feita no veículo (revisão, óleo, pneus, freios...) com o custo real e, se houver,
/// quando deve ser a próxima (por km e/ou data) — vira aviso de "vencida"/"em breve".
/// </summary>
public class Manutencao : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required Guid VeiculoId { get; set; }
    public Vehicle? Veiculo { get; set; }

    public DateOnly Data { get; set; }
    public int Km { get; set; }

    /// <summary>Tipo em CAIXA ALTA (REVISÃO, TROCA DE ÓLEO, PNEUS...) — agrupa as próximas.</summary>
    public required string Tipo { get; set; }

    public string? Descricao { get; set; }
    public decimal Valor { get; set; }
    public string? Oficina { get; set; }

    public int? ProximaKm { get; set; }
    public DateOnly? ProximaData { get; set; }
}
