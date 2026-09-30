using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManutencaoComItensENota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "Manutencoes",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "AnexoId",
                table: "Manutencoes",
                type: "char(36)",
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<decimal>(
                name: "MaoDeObra",
                table: "Manutencoes",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ManutencaoAnexos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_bin"),
                    Nome = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Conteudo = table.Column<byte[]>(type: "longblob", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManutencaoAnexos", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ManutencaoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    ManutencaoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    Quantidade = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Descricao = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManutencaoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManutencaoItens_Manutencoes_ManutencaoId",
                        column: x => x.ManutencaoId,
                        principalTable: "Manutencoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ManutencaoProximas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    ManutencaoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_bin"),
                    Tipo = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProximaKm = table.Column<int>(type: "int", nullable: true),
                    ProximaData = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManutencaoProximas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManutencaoProximas_Manutencoes_ManutencaoId",
                        column: x => x.ManutencaoId,
                        principalTable: "Manutencoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ManutencaoItens_ManutencaoId",
                table: "ManutencaoItens",
                column: "ManutencaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ManutencaoProximas_ManutencaoId",
                table: "ManutencaoProximas",
                column: "ManutencaoId");
        
            // Dados antigos: a "próxima" de cada manutenção vai pra tabela nova, e o valor vira um item
            // (assim o total continua igual). Só depois as colunas antigas saem.
            migrationBuilder.Sql(@"INSERT INTO ManutencaoProximas (Id, ManutencaoId, Tipo, ProximaKm, ProximaData, CreatedAt, UpdatedAt)
                SELECT UUID(), Id, LEFT(Tipo, 80), ProximaKm, ProximaData, UTC_TIMESTAMP(6), NULL FROM Manutencoes
                WHERE ProximaKm IS NOT NULL OR ProximaData IS NOT NULL;");
            migrationBuilder.Sql(@"INSERT INTO ManutencaoItens (Id, ManutencaoId, Quantidade, Descricao, Valor, CreatedAt, UpdatedAt)
                SELECT UUID(), Id, 1, LEFT(COALESCE(Descricao, Tipo), 300), Valor, UTC_TIMESTAMP(6), NULL FROM Manutencoes;");

            migrationBuilder.DropColumn(
                name: "ProximaData",
                table: "Manutencoes");

            migrationBuilder.DropColumn(
                name: "ProximaKm",
                table: "Manutencoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManutencaoAnexos");

            migrationBuilder.DropTable(
                name: "ManutencaoItens");

            migrationBuilder.DropTable(
                name: "ManutencaoProximas");

            migrationBuilder.DropColumn(
                name: "AnexoId",
                table: "Manutencoes");

            migrationBuilder.DropColumn(
                name: "MaoDeObra",
                table: "Manutencoes");

            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "Manutencoes",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProximaData",
                table: "Manutencoes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProximaKm",
                table: "Manutencoes",
                type: "int",
                nullable: true);
        }
    }
}
