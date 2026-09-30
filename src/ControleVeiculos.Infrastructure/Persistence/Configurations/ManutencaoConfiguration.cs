using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Configurations;

public class ManutencaoConfiguration : IEntityTypeConfiguration<Manutencao>
{
    public void Configure(EntityTypeBuilder<Manutencao> builder)
    {
        builder.ToTable("Manutencoes");
        builder.Property(m => m.Tipo).HasMaxLength(80);
        builder.Property(m => m.Descricao).HasMaxLength(1000);
        builder.Property(m => m.Oficina).HasMaxLength(200);
        builder.Property(m => m.Valor).HasPrecision(10, 2);
        builder.HasIndex(m => new { m.VeiculoId, m.Data });
        builder.HasOne(m => m.Veiculo).WithMany().HasForeignKey(m => m.VeiculoId).OnDelete(DeleteBehavior.Restrict);
    }
}
