namespace ControleVeiculos.Shared.Auth;

/// <summary>
/// <paramref name="Login"/> é o identificador curto usado pra entrar (ex: "DANI") — se omitido,
/// usa o e-mail como login (comportamento antigo, usado por Admin/Gestor). O e-mail continua
/// obrigatório (exigência do ASP.NET Identity), mesmo que seja só um placeholder interno pra
/// contas de motorista que não têm e-mail de verdade cadastrado.
/// </summary>
public record RegisterRequest(string NomeCompleto, string Email, string Password, string Role, string? Login = null);
