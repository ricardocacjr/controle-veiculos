using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Configurations;

public class MotivoUsoConfiguration : IEntityTypeConfiguration<MotivoUso>
{
    public void Configure(EntityTypeBuilder<MotivoUso> builder)
    {
        builder.Property(m => m.Nome).HasMaxLength(200);
        builder.HasIndex(m => m.Nome).IsUnique();
    }
}
