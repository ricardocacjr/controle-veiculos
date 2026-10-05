using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ControleVeiculos.Domain.Entities;

namespace ControleVeiculos.Infrastructure.Persistence.Configurations;

public class ManutencaoConfiguration : IEntityTypeConfiguration<Manutencao>
{
    public void Configure(EntityTypeBuilder<Manutencao> builder)
    {
        builder.ToTable("Manutencoes");
        builder.Property(m => m.Tipo).HasMaxLength(200);
        builder.Property(m => m.Descricao).HasMaxLength(1000);
        builder.Property(m => m.Oficina).HasMaxLength(200);
        builder.Property(m => m.Valor).HasPrecision(10, 2);
        builder.Property(m => m.MaoDeObra).HasPrecision(10, 2);
        builder.HasIndex(m => new { m.VeiculoId, m.Data });
        builder.HasOne(m => m.Veiculo).WithMany().HasForeignKey(m => m.VeiculoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(m => m.Itens).WithOne().HasForeignKey(i => i.ManutencaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.Proximas).WithOne().HasForeignKey(p => p.ManutencaoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ManutencaoItemConfiguration : IEntityTypeConfiguration<ManutencaoItem>
{
    public void Configure(EntityTypeBuilder<ManutencaoItem> builder)
    {
        builder.ToTable("ManutencaoItens");
        builder.Property(i => i.Descricao).HasMaxLength(300);
        builder.Property(i => i.Quantidade).HasPrecision(10, 2);
        builder.Property(i => i.Valor).HasPrecision(10, 2);
    }
}

public class ManutencaoProximaConfiguration : IEntityTypeConfiguration<ManutencaoProxima>
{
    public void Configure(EntityTypeBuilder<ManutencaoProxima> builder)
    {
        builder.ToTable("ManutencaoProximas");
        builder.Property(p => p.Tipo).HasMaxLength(80);
    }
}

public class AnexoConfiguration : IEntityTypeConfiguration<Anexo>
{
    public void Configure(EntityTypeBuilder<Anexo> builder)
    {
        builder.ToTable("Anexos");
        builder.Property(a => a.Nome).HasMaxLength(200);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.Conteudo).HasColumnType("longblob");
    }
}
