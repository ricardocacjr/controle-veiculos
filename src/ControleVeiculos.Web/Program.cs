using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using ControleVeiculos.Web.Components;
using ControleVeiculos.Web.Services;

// O container do Render roda com cultura invariante (inglês): sem isso "80.007 km" vira "80,007 km"
// e "R$ 5,49" vira "R$ 5.49". O painel é só pt-BR. (Horário: ver Rotulos.Local — fixo em Brasília.)
var ptBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    // Celular sai do app (câmera, WhatsApp) e a conexão cai; guarda a tela do motorista por mais
    // tempo pra ele voltar exatamente de onde parou (padrão: 3 min).
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(15);
});

// O InputFile (upload de foto/áudio na tela do motorista) transfere o arquivo do navegador pro
// servidor pelo mesmo circuito SignalR do Blazor Server — sem aumentar isso, o limite padrão
// (32KB) trava upload de foto de celular bem antes dos 20MB que liberamos no InputFile.
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 20 * 1024 * 1024;
});

builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<Localizacao>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Base").Get<BaseOperacional>() ?? new BaseOperacional());
builder.Services.AddHttpClient<ControleVeiculosApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("Configuração 'ApiBaseUrl' não definida em appsettings.json.");
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Mesmo motivo do Program.cs da Api: atrás do proxy de borda do Render, a requisição chega
// como HTTP puro, então precisamos confiar no X-Forwarded-Proto pra saber que era HTTPS.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
