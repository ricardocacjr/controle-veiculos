using System.Text.RegularExpressions;
using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>Catálogo de finalidades/motivos de uso do veículo (ex: "ENTREGA DE MÓVEIS",
/// "TRASLADO PESSOAL") — cresce sozinho: toda vez que um uso é iniciado com uma finalidade nova,
/// ela entra aqui automaticamente pra aparecer como sugestão da próxima vez.</summary>
public class MotivoUso : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required string Nome { get; set; }

    /// <summary>
    /// Padrão dos motivos: CAIXA ALTA, sem espaços sobrando — assim "Casa", "casa " e "CASA" são
    /// o mesmo motivo nos relatórios. Usado em tudo que grava motivo (saída, edição, catálogo).
    /// </summary>
    public static string Normalizar(string? nome) =>
        Regex.Replace(nome ?? "", @"\s+", " ").Trim().ToUpperInvariant();
}
