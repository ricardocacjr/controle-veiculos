using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ControleVeiculos.Application.Interfaces;
using ControleVeiculos.Infrastructure.Geocoding;
using ControleVeiculos.Infrastructure.Identity;
using ControleVeiculos.Infrastructure.Ocr;
using ControleVeiculos.Infrastructure.Persistence;
using ControleVeiculos.Infrastructure.Persistence.Repositories;
using ControleVeiculos.Infrastructure.Transcription;

namespace ControleVeiculos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Motoristas entram com PIN numérico de 6 dígitos (perfil + PIN, estilo app de
                // banco). O bloqueio após tentativas erradas (lockout do Identity) compensa o
                // espaço menor de combinações.
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IUsageRecordRepository, UsageRecordRepository>();
        services.AddScoped<IEmpresaRepository, EmpresaRepository>();
        services.AddScoped<IMotivoUsoRepository, MotivoUsoRepository>();
        services.AddScoped<IVoiceTranscriptionService, GoogleSpeechTranscriptionService>();
        services.AddScoped<IOdometerOcrService, GoogleVisionOdometerOcrService>();
        services.AddScoped<IFuelReceiptOcrService, GoogleVisionFuelReceiptOcrService>();

        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            // Exigido pela política de uso do Nominatim: identificar a aplicação no User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ControleVeiculos/1.0 (app interno de frota)");
        });

        return services;
    }
}
