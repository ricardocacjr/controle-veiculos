using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Configurations;

public class FuelEntryConfiguration : IEntityTypeConfiguration<FuelEntry>
{
    public void Configure(EntityTypeBuilder<FuelEntry> builder)
    {
        // Bomba marca litros com 3 casas (19,416 L) — com 2 virava 19,42.
        builder.Property(f => f.Litros).HasPrecision(12, 3);
        builder.Property(f => f.ValorTotal).HasPrecision(10, 2);
        builder.Property(f => f.Desconto).HasPrecision(10, 2);
        builder.Property(f => f.ValorPorLitro).HasPrecision(10, 3);
        builder.Property(f => f.TanqueCheio).HasDefaultValue(true);
    }
}
