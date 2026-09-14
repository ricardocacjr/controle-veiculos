using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Configurations;

public class FuelEntryConfiguration : IEntityTypeConfiguration<FuelEntry>
{
    public void Configure(EntityTypeBuilder<FuelEntry> builder)
    {
        builder.Property(f => f.Litros).HasPrecision(10, 2);
        builder.Property(f => f.ValorTotal).HasPrecision(10, 2);
    }
}
