using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

/// <summary>Empresa/centro de custo pelo qual um uso de veículo é feito (ex: "Voglio", "OAK",
/// "Uso Pessoal") — cadastro gerenciado à parte, igual veículo, não uma lista que cresce sozinha
/// como <see cref="MotivoUso"/>.</summary>
public class Empresa : BaseEntity, ITenantScoped
{
    public Guid? TenantId { get; set; }

    public required string Nome { get; set; }
}
