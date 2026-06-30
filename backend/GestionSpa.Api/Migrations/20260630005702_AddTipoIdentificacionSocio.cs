using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionSpa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoIdentificacionSocio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoIdentificacion",
                table: "Socios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoIdentificacion",
                table: "Socios");
        }
    }
}
