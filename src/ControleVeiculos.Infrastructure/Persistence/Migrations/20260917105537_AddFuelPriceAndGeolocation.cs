using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFuelPriceAndGeolocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LatitudeInicial",
                table: "UsageRecords",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeInicial",
                table: "UsageRecords",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorPorLitro",
                table: "FuelEntries",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatitudeInicial",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "LongitudeInicial",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "ValorPorLitro",
                table: "FuelEntries");
        }
    }
}
