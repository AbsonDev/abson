using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fisga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrivacidadeDoLocal",
                schema: "fisga",
                table: "pescarias",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Exato");

            migrationBuilder.Sql("UPDATE fisga.pescarias SET \"PrivacidadeDoLocal\" = 'Exato' WHERE \"PrivacidadeDoLocal\" IS NULL OR \"PrivacidadeDoLocal\" = '';");

            migrationBuilder.AddColumn<string>(
                name: "Fotos",
                schema: "fisga",
                table: "pescaria_capturas",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivacidadeDoLocal",
                schema: "fisga",
                table: "pescarias");

            migrationBuilder.DropColumn(
                name: "Fotos",
                schema: "fisga",
                table: "pescaria_capturas");
        }
    }
}
