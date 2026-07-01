using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionSpa.Api.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLocalidadPendienteSocios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Socios"
                SET "Ciudad" = 'Pendiente agregar localidad'
                WHERE "Ciudad" IS NULL
                   OR TRIM("Ciudad") = ''
                   OR "Ciudad" = 'Salto';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
