using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fisga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Contas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pré-lançamento: não há usuários reais e os dados de teste não têm dono.
            // Limpamos as pescarias antes de tornar UsuarioId obrigatório.
            migrationBuilder.Sql("DELETE FROM fisga.pescarias;");

            migrationBuilder.DropIndex(
                name: "IX_pescarias_DiarioTokenHash",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.DropColumn(
                name: "DiarioTokenHash",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.DropColumn(
                name: "EditTokenHash",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                schema: "fisga",
                table: "pescarias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "tokens_de_login",
                schema: "fisga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens_de_login", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "fisga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AceitaNovidades = table.Column<bool>(type: "boolean", nullable: false),
                    EmailVerificadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UltimoAcessoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BemVindoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sessoes",
                schema: "fisga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UltimoUsoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevogadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sessoes_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "fisga",
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pescarias_UsuarioId",
                schema: "fisga",
                table: "pescarias",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_sessoes_TokenHash",
                schema: "fisga",
                table: "sessoes",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessoes_UsuarioId_ExpiraEm",
                schema: "fisga",
                table: "sessoes",
                columns: new[] { "UsuarioId", "ExpiraEm" });

            migrationBuilder.CreateIndex(
                name: "IX_tokens_de_login_Email_CriadoEm",
                schema: "fisga",
                table: "tokens_de_login",
                columns: new[] { "Email", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_tokens_de_login_TokenHash",
                schema: "fisga",
                table: "tokens_de_login",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_email",
                schema: "fisga",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_pescarias_usuarios_UsuarioId",
                schema: "fisga",
                table: "pescarias",
                column: "UsuarioId",
                principalSchema: "fisga",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pescarias_usuarios_UsuarioId",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.DropTable(
                name: "sessoes",
                schema: "fisga");

            migrationBuilder.DropTable(
                name: "tokens_de_login",
                schema: "fisga");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "fisga");

            migrationBuilder.DropIndex(
                name: "IX_pescarias_UsuarioId",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.AddColumn<string>(
                name: "DiarioTokenHash",
                schema: "fisga",
                table: "pescarias",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EditTokenHash",
                schema: "fisga",
                table: "pescarias",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_pescarias_DiarioTokenHash",
                schema: "fisga",
                table: "pescarias",
                column: "DiarioTokenHash");
        }
    }
}
