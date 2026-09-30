using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItemDaNotaComOrdem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ordem",
                table: "ManutencaoItens",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ordem",
                table: "ManutencaoItens");
        }
    }
}
