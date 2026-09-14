using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControleVeiculos.Infrastructure.Persistence;

/// <summary>
/// Só usada pelas ferramentas de design-time do EF Core (dotnet ef migrations add/update) quando
/// não há um servidor MySQL acessível pra fazer o AutoDetect de versão que o app usa em runtime
/// (ver <see cref="DependencyInjection"/>). A versão fixa aqui não precisa bater exatamente com a
/// do servidor real — só serve pra gerar o SQL das migrations.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Port=3306;Database=controle_veiculos_db;",
            new MySqlServerVersion(new Version(8, 0, 35)));

        return new AppDbContext(optionsBuilder.Options);
    }
}
