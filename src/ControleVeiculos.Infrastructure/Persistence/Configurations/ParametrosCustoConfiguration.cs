using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Configurations;

public class ParametrosCustoConfiguration : IEntityTypeConfiguration<ParametrosCusto>
{
    public void Configure(EntityTypeBuilder<ParametrosCusto> builder)
    {
        builder.ToTable("ParametrosCusto");
        builder.Property(p => p.TarifaPorKm).HasPrecision(10, 4);
        builder.Property(p => p.PrecoJogoPneus).HasPrecision(10, 2);
        builder.Property(p => p.ManutencaoPorKm).HasPrecision(10, 4);
        builder.Ignore(p => p.PneusPorKm);
        builder.Ignore(p => p.ExtraPorKm);
    }
}
