using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ControleVeiculos.Api.Auth;
using ControleVeiculos.Infrastructure;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Infrastructure.Persistence;

// Sem isso, model binding de [FromForm] double (ex: latitude/longitude em LerPainel) interpreta
// "-23.5613" com as regras de decimal do SO do servidor — em pt-BR isso vira "-235613" (ponto
// tratado como separador de milhar), corrompendo qualquer coordenada enviada. Api não tem saída
// formatada pra usuário (JSON é sempre invariante), então não há motivo pra usar a cultura local
// aqui — nem no bind de entrada nem em qualquer formatação futura.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Seção 'Jwt' não configurada em appsettings.json.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Atrás do proxy de borda do Render (ou qualquer PaaS que termina TLS antes do container), a
// requisição chega ao Kestrel como HTTP puro. Sem isso, UseHttpsRedirection não sabe que o
// pedido original era HTTPS. O proxy de entrada é sempre confiável aqui (não há outro caminho
// público até a porta do container), por isso limpamos as listas de proxy/rede conhecidos.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok("ControleVeiculos.Api"));

// Alvo do ping periódico (MikroTik) que mantém acordados o Render (dorme após ~15 min sem
// requisição) e o MySQL free do Aiven (desliga por inatividade de banco — um GET que não toca no
// banco não adianta, por isso o SELECT 1 aqui).
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        return Results.Ok("ok");
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapControllers();

// Permite gerar migrations (dotnet ef) sem um banco acessível — o host sobe mas não migra/semeia.
if (Environment.GetEnvironmentVariable("SKIP_DB_INIT") != "1")
    await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }

    // Cadastro agora é só pelo Admin — num banco novo ninguém conseguiria criar o primeiro.
    // BootstrapAdmin__Email/BootstrapAdmin__Senha criam esse primeiro Admin (só se não houver nenhum).
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var email = app.Configuration["BootstrapAdmin:Email"];
    var senha = app.Configuration["BootstrapAdmin:Senha"];
    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(senha)
        && (await userManager.GetUsersInRoleAsync(Roles.Admin)).Count == 0)
    {
        var admin = new ApplicationUser { UserName = email, Email = email, NomeCompleto = "Administrador" };
        if ((await userManager.CreateAsync(admin, senha)).Succeeded)
            await userManager.AddToRoleAsync(admin, Roles.Admin);
    }
}
