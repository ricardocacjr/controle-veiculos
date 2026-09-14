namespace ControleVeiculos.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a tenant. Null while the system runs single-tenant;
/// populated once multi-tenant support is switched on for the product offering.
/// </summary>
public interface ITenantScoped
{
    Guid? TenantId { get; set; }
}
