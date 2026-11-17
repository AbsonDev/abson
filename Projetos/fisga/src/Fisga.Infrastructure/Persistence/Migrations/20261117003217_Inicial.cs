using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fisga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fisga");

            migrationBuilder.CreateTable(
                name: "pescarias",
                schema: "fisga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiarioTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EditTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Local = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Pescador = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Cidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Ambiente = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Periodo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Mare = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Lua = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Clima = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClarezaDaAgua = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pescarias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pescaria_capturas",
                schema: "fisga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PescariaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Especie = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ComprimentoCm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Isca = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Solto = table.Column<bool>(type: "boolean", nullable: false),
                    FotoUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pescaria_capturas", x => new { x.PescariaId, x.Id });
                    table.ForeignKey(
                        name: "FK_pescaria_capturas_pescarias_PescariaId",
                        column: x => x.PescariaId,
                        principalSchema: "fisga",
                        principalTable: "pescarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pescarias_DiarioTokenHash",
                schema: "fisga",
                table: "pescarias",
                column: "DiarioTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_pescarias_ShareToken",
                schema: "fisga",
                table: "pescarias",
                column: "ShareToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pescaria_capturas",
                schema: "fisga");

            migrationBuilder.DropTable(
                name: "pescarias",
                schema: "fisga");
        }
    }
}
