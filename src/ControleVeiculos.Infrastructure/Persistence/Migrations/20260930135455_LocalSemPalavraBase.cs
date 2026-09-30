using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleVeiculos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// "Saiu de" / "Chegou em" ficam só com o endereço: tira o prefixo "Base - " que o app
    /// gravava quando a saída/chegada era na base (pedido do admin).
    /// </summary>
    public partial class LocalSemPalavraBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE UsageRecords SET Origem = TRIM(SUBSTRING(Origem, 8)) WHERE Origem LIKE 'Base - %';");
            migrationBuilder.Sql("UPDATE UsageRecords SET Destino = TRIM(SUBSTRING(Destino, 8)) WHERE Destino LIKE 'Base - %';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
