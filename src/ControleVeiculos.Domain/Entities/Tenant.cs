using ControleVeiculos.Domain.Common;

namespace ControleVeiculos.Domain.Entities;

public class Tenant : BaseEntity
{
    public required string Nome { get; set; }
}
