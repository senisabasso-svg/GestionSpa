using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionSpa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMostrarControlIngreso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default true: las empresas existentes siguen viendo el kiosk.
            migrationBuilder.AddColumn<bool>(
                name: "MostrarControlIngreso",
                table: "Emisores",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Daymán usa portero biométrico: ocultar Control de Ingreso.
            migrationBuilder.Sql("""
                UPDATE "Emisores" SET "MostrarControlIngreso" = FALSE WHERE "Slug" = 'dayman';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MostrarControlIngreso",
                table: "Emisores");
        }
    }
}
