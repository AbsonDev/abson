using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abson.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "career_ops_actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    Result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_ops_actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "career_ops_snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "career-ops"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_ops_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_career_ops_actions_CreatedAt",
                table: "career_ops_actions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_career_ops_actions_Status",
                table: "career_ops_actions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "career_ops_actions");

            migrationBuilder.DropTable(
                name: "career_ops_snapshots");
        }
    }
}
