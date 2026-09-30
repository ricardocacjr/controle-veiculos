using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManutencaoDoVeiculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Manutencoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_bin"),
                    VeiculoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Km = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descricao = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Oficina = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProximaKm = table.Column<int>(type: "int", nullable: true),
                    ProximaData = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manutencoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Manutencoes_Vehicles_VeiculoId",
                        column: x => x.VeiculoId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Manutencoes_VeiculoId_Data",
                table: "Manutencoes",
                columns: new[] { "VeiculoId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Manutencoes");
        }
    }
}
