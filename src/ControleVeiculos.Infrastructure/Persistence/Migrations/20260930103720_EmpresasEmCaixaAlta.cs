using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Empresas em CAIXA ALTA (igual aos motivos). Se duas ficarem com o mesmo nome ("Oak" e "OAK"),
    /// as saídas da repetida passam pra que fica antes de juntar — nenhuma saída perde a empresa.
    /// Daqui pra frente a Api já grava em maiúsculas (Empresa.Normalizar).
    /// </summary>
    public partial class EmpresasEmCaixaAlta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE UsageRecords u
                JOIN Empresas repetida ON repetida.Id = u.EmpresaId
                JOIN Empresas fica ON UPPER(TRIM(fica.Nome)) = UPPER(TRIM(repetida.Nome)) AND fica.Id < repetida.Id
                SET u.EmpresaId = fica.Id;");
            migrationBuilder.Sql(@"DELETE repetida FROM Empresas repetida
                JOIN Empresas fica ON UPPER(TRIM(fica.Nome)) = UPPER(TRIM(repetida.Nome)) AND fica.Id < repetida.Id;");
            migrationBuilder.Sql("UPDATE Empresas SET Nome = UPPER(TRIM(Nome));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: a grafia original não é guardada.
        }
    }
}
