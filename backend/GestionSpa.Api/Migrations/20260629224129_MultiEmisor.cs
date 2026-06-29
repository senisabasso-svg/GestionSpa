using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestionSpa.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultiEmisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Socios_Cedula",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Socios_NumeroSocio",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Familias_Nombre",
                table: "Familias");

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Socios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Servicios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Pagos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Ingresos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Familias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "CuotasMensuales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Clientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmisorId",
                table: "Cargos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Emisores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Ciudad = table.Column<string>(type: "text", nullable: true),
                    Departamento = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emisores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    EmisorId = table.Column<int>(type: "integer", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Emisores_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "Emisores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Socios_EmisorId_Cedula",
                table: "Socios",
                columns: new[] { "EmisorId", "Cedula" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Socios_EmisorId_NumeroSocio",
                table: "Socios",
                columns: new[] { "EmisorId", "NumeroSocio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servicios_EmisorId",
                table: "Servicios",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_EmisorId",
                table: "Pagos",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_EmisorId",
                table: "Ingresos",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Familias_EmisorId_Nombre",
                table: "Familias",
                columns: new[] { "EmisorId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuotasMensuales_EmisorId",
                table: "CuotasMensuales",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmisorId",
                table: "Clientes",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_EmisorId",
                table: "Cargos",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Emisores_Nombre",
                table: "Emisores",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Emisores_Slug",
                table: "Emisores",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmisorId",
                table: "Usuarios",
                column: "EmisorId");

            migrationBuilder.Sql("""
                INSERT INTO "Emisores" ("Nombre", "Slug", "Ciudad", "Departamento", "Activo", "FechaAlta")
                SELECT 'SPA Thermal Daymán', 'dayman', 'Salto', 'Salto', true, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM "Emisores");

                UPDATE "Socios" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "Servicios" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "Clientes" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "Familias" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "CuotasMensuales" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "Cargos" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "Pagos" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                UPDATE "Ingresos" SET "EmisorId" = (SELECT "Id" FROM "Emisores" WHERE "Slug" = 'dayman' LIMIT 1) WHERE "EmisorId" = 0;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Cargos_Emisores_EmisorId",
                table: "Cargos",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Emisores_EmisorId",
                table: "Clientes",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CuotasMensuales_Emisores_EmisorId",
                table: "CuotasMensuales",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Familias_Emisores_EmisorId",
                table: "Familias",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingresos_Emisores_EmisorId",
                table: "Ingresos",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Emisores_EmisorId",
                table: "Pagos",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Servicios_Emisores_EmisorId",
                table: "Servicios",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Socios_Emisores_EmisorId",
                table: "Socios",
                column: "EmisorId",
                principalTable: "Emisores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cargos_Emisores_EmisorId",
                table: "Cargos");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Emisores_EmisorId",
                table: "Clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_CuotasMensuales_Emisores_EmisorId",
                table: "CuotasMensuales");

            migrationBuilder.DropForeignKey(
                name: "FK_Familias_Emisores_EmisorId",
                table: "Familias");

            migrationBuilder.DropForeignKey(
                name: "FK_Ingresos_Emisores_EmisorId",
                table: "Ingresos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Emisores_EmisorId",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_Servicios_Emisores_EmisorId",
                table: "Servicios");

            migrationBuilder.DropForeignKey(
                name: "FK_Socios_Emisores_EmisorId",
                table: "Socios");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Emisores");

            migrationBuilder.DropIndex(
                name: "IX_Socios_EmisorId_Cedula",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Socios_EmisorId_NumeroSocio",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Servicios_EmisorId",
                table: "Servicios");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_EmisorId",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Ingresos_EmisorId",
                table: "Ingresos");

            migrationBuilder.DropIndex(
                name: "IX_Familias_EmisorId_Nombre",
                table: "Familias");

            migrationBuilder.DropIndex(
                name: "IX_CuotasMensuales_EmisorId",
                table: "CuotasMensuales");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EmisorId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Cargos_EmisorId",
                table: "Cargos");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Servicios");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Ingresos");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Familias");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "CuotasMensuales");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EmisorId",
                table: "Cargos");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_Cedula",
                table: "Socios",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Socios_NumeroSocio",
                table: "Socios",
                column: "NumeroSocio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Familias_Nombre",
                table: "Familias",
                column: "Nombre",
                unique: true);
        }
    }
}
