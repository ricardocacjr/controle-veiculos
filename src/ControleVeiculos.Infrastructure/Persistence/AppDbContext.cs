using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ControleVeiculos.Domain.Entities;
using ControleVeiculos.Infrastructure.Identity;

namespace ControleVeiculos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<VehiclePhoto> VehiclePhotos => Set<VehiclePhoto>();
    public DbSet<VoiceNote> VoiceNotes => Set<VoiceNote>();
    public DbSet<FuelEntry> FuelEntries => Set<FuelEntry>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<MotivoUso> MotivosUso => Set<MotivoUso>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
