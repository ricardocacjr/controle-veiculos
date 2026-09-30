using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalDeChegada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LatitudeFinal",
                table: "UsageRecords",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeFinal",
                table: "UsageRecords",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatitudeFinal",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "LongitudeFinal",
                table: "UsageRecords");
        }
    }
}
