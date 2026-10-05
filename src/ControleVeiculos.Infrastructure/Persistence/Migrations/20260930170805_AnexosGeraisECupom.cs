using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Anexos passam a ser gerais (nota da oficina e cupom do posto): a tabela ManutencaoAnexos é
    /// RENOMEADA pra Anexos — o EF queria apagar e recriar, o que perderia as notas já guardadas.
    /// Abastecimento ganha o cupom (ComprovanteId). Oficinas das manutenções em CAIXA ALTA.
    /// </summary>
    public partial class AnexosGeraisECupom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "ManutencaoAnexos", newName: "Anexos");

            migrationBuilder.AddColumn<Guid>(
                name: "ComprovanteId",
                table: "FuelEntries",
                type: "char(36)",
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.Sql("UPDATE Manutencoes SET Oficina = UPPER(TRIM(Oficina)) WHERE Oficina IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ComprovanteId", table: "FuelEntries");
            migrationBuilder.RenameTable(name: "Anexos", newName: "ManutencaoAnexos");
        }
    }
}
