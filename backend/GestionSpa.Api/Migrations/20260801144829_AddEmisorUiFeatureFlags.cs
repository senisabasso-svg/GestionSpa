using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionSpa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmisorUiFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MostrarConfigPortero",
                table: "Emisores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarSorteo",
                table: "Emisores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Defaults seguros (false). Solo se habilitan emisores conocidos sin tocar el resto.
            migrationBuilder.Sql("""
                UPDATE "Emisores" SET "MostrarConfigPortero" = TRUE WHERE "Slug" = 'dayman';
                UPDATE "Emisores" SET "MostrarSorteo" = TRUE WHERE "Slug" = 'anepa';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MostrarConfigPortero",
                table: "Emisores");

            migrationBuilder.DropColumn(
                name: "MostrarSorteo",
                table: "Emisores");
        }
    }
}
