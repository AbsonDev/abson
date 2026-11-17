using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fisga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trilhas_privadas",
                schema: "fisga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Pontos = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trilhas_privadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trilhas_privadas_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "fisga",
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trilhas_privadas_UsuarioId",
                schema: "fisga",
                table: "trilhas_privadas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trilhas_privadas",
                schema: "fisga");
        }
    }
}
