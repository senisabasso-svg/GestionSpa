using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestionSpa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPorteroUsuariosExtraidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PorteroUsuariosExtraidos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmisorId = table.Column<int>(type: "integer", nullable: false),
                    Pin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Privilegio = table.Column<int>(type: "integer", nullable: false),
                    Tarjeta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeviceSn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PrimeraExtraccionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimaExtraccionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PorteroUsuariosExtraidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PorteroUsuariosExtraidos_Emisores_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "Emisores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PorteroUsuariosExtraidos_EmisorId_Pin",
                table: "PorteroUsuariosExtraidos",
                columns: new[] { "EmisorId", "Pin" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PorteroUsuariosExtraidos");
        }
    }
}
