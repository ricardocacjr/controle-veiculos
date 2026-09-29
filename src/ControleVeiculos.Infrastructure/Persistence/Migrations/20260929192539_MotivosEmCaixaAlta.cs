using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Motivos padronizados em CAIXA ALTA (pedido do admin): converte os motivos das saídas e do
    /// catálogo. No catálogo, nomes que ficam iguais depois da conversão ("Casa" e "CASA") viram um
    /// só — o índice único de Nome não aceitaria os dois. Daqui pra frente a Api já grava em
    /// maiúsculas (MotivoUso.Normalizar).
    /// </summary>
    public partial class MotivosEmCaixaAlta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE UsageRecords SET Finalidade = UPPER(TRIM(Finalidade));");
            migrationBuilder.Sql(@"DELETE m FROM MotivosUso m
                JOIN MotivosUso o ON UPPER(TRIM(o.Nome)) = UPPER(TRIM(m.Nome)) AND o.Id < m.Id;");
            migrationBuilder.Sql("UPDATE MotivosUso SET Nome = UPPER(TRIM(Nome));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: a grafia original não é guardada.
        }
    }
}
