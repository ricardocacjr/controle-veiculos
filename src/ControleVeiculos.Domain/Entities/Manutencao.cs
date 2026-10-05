using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>
/// Manutenção feita no veículo, no formato da nota da oficina: itens (qtd, descrição, valor),
/// mão de obra e total. As "próximas" (troca de óleo, filtros, pneus...) são sugeridas pelos itens
/// e viram aviso de vencida / em breve. A nota (PDF ou foto) fica guardada como anexo.
/// </summary>
public class Manutencao : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required Guid VeiculoId { get; set; }
    public Vehicle? Veiculo { get; set; }

    public DateOnly Data { get; set; }
    public int Km { get; set; }

    /// <summary>Resumo em CAIXA ALTA, montado pelas categorias dos itens (ex.: "TROCA DE ÓLEO, FILTROS, PALHETAS, OUTROS").</summary>
    public required string Tipo { get; set; }

    /// <summary>Observação da nota / anotação livre.</summary>
    public string? Descricao { get; set; }

    /// <summary>Total da nota = soma dos itens + mão de obra.</summary>
    public decimal Valor { get; set; }
    public decimal MaoDeObra { get; set; }
    public string? Oficina { get; set; }

    public Guid? AnexoId { get; set; }

    public ICollection<ManutencaoItem> Itens { get; set; } = new List<ManutencaoItem>();
    public ICollection<ManutencaoProxima> Proximas { get; set; } = new List<ManutencaoProxima>();
}

/// <summary>Linha da nota: quantidade, descrição e valor (total da linha).</summary>
public class ManutencaoItem : BaseEntity
{
    public required Guid ManutencaoId { get; set; }
    /// <summary>Posição do item na nota (mostra na mesma ordem da oficina).</summary>
    public int Ordem { get; set; }
    public decimal Quantidade { get; set; } = 1;
    public required string Descricao { get; set; }
    public decimal Valor { get; set; }
}

/// <summary>Quando deve ser a próxima de uma categoria (ex.: TROCA DE ÓLEO aos 183.389 km ou em 30/09/2027).</summary>
public class ManutencaoProxima : BaseEntity
{
    public required Guid ManutencaoId { get; set; }
    public required string Tipo { get; set; }
    public int? ProximaKm { get; set; }
    public DateOnly? ProximaData { get; set; }
}
