using Microsoft.AspNetCore.Identity;

namespace ControleVeiculos.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string NomeCompleto { get; set; }
    public Guid? TenantId { get; set; }
}
