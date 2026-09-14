namespace ControleVeiculos.Infrastructure.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Gestor = "Gestor";
    public const string Motorista = "Motorista";

    public static readonly string[] All = [Admin, Gestor, Motorista];
}
