using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ControleVeiculos.Application.Interfaces;
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
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Exigir maiúscula + caractere especial (padrão do Identity) é fricção
                // desnecessária pra motoristas digitando a senha no celular em campo — mantém
                // dígito + minúscula + 8 caracteres, que já é um mínimo razoável.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IUsageRecordRepository, UsageRecordRepository>();
        services.AddScoped<IVoiceTranscriptionService, GoogleSpeechTranscriptionService>();
        services.AddScoped<IOdometerOcrService, GoogleVisionOdometerOcrService>();

        return services;
    }
}
