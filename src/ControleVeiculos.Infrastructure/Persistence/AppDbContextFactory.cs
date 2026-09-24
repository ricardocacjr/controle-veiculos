using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControleVeiculos.Infrastructure.Persistence;

/// <summary>
/// Só usada pelas ferramentas de design-time do EF Core (dotnet ef migrations add/update).
/// Um <see cref="IDesignTimeDbContextFactory{TContext}"/> na Infrastructure tem prioridade sobre
/// o startup project passado em --startup-project, então é aqui (não em appsettings.Development.json
/// da Api) que a connection string de dev precisa bater com o banco/usuário criados localmente
/// (ver README, seção "Banco de dados"). Se não houver um MySQL acessível neste endereço na hora
/// de gerar uma migration nova, troque temporariamente por uma versão fixa
/// (ex: new MySqlServerVersion(new Version(8, 0, 35))) em vez de AutoDetect.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=controle_veiculos_db;User=controle_veiculos_app;Password=ControleVeiculos_App_2026!;";

    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 35)));

        return new AppDbContext(optionsBuilder.Options);
    }
}
