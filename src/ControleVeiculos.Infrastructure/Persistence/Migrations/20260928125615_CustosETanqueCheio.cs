using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustosETanqueCheio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Litros",
                table: "FuelEntries",
                type: "decimal(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<bool>(
                name: "PagoPeloMotorista",
                table: "FuelEntries",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TanqueCheio",
                table: "FuelEntries",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ParametrosCusto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_bin"),
                    TarifaPorKm = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PrecoJogoPneus = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    VidaUtilPneusKm = table.Column<int>(type: "int", nullable: false),
                    ManutencaoPorKm = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosCusto", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosCusto");

            migrationBuilder.DropColumn(
                name: "PagoPeloMotorista",
                table: "FuelEntries");

            migrationBuilder.DropColumn(
                name: "TanqueCheio",
                table: "FuelEntries");

            migrationBuilder.AlterColumn<decimal>(
                name: "Litros",
                table: "FuelEntries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,3)",
                oldPrecision: 12,
                oldScale: 3);
        }
    }
}
