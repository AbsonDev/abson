using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardapioNoLucro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cardapio");

            migrationBuilder.CreateTable(
                name: "fichas",
                schema: "cardapio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EditTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NomeDoPrato = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Estabelecimento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Categoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RendimentoEmPorcoes = table.Column<int>(type: "integer", nullable: false),
                    PorcoesVendidasPorMes = table.Column<int>(type: "integer", nullable: false),
                    MinutosDeProducao = table.Column<int>(type: "integer", nullable: false),
                    ValorHoraMaoDeObra = table.Column<long>(type: "bigint", nullable: false),
                    CustosFixosMensais = table.Column<long>(type: "bigint", nullable: false),
                    Imposto = table.Column<int>(type: "integer", nullable: false),
                    CmvAlvo = table.Column<int>(type: "integer", nullable: false),
                    Arredondamento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fichas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ficha_canais",
                schema: "cardapio",
                columns: table => new
                {
                    FichaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comissao = table.Column<int>(type: "integer", nullable: false),
                    TaxaFixaPorPedido = table.Column<long>(type: "bigint", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ficha_canais", x => new { x.FichaId, x.Id });
                    table.ForeignKey(
                        name: "FK_ficha_canais_fichas_FichaId",
                        column: x => x.FichaId,
                        principalSchema: "cardapio",
                        principalTable: "fichas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ficha_embalagens",
                schema: "cardapio",
                columns: table => new
                {
                    FichaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CustoPorPorcao = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ficha_embalagens", x => new { x.FichaId, x.Id });
                    table.ForeignKey(
                        name: "FK_ficha_embalagens_fichas_FichaId",
                        column: x => x.FichaId,
                        principalSchema: "cardapio",
                        principalTable: "fichas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ficha_ingredientes",
                schema: "cardapio",
                columns: table => new
                {
                    FichaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PrecoUnitario = table.Column<long>(type: "bigint", nullable: false),
                    Rendimento = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ficha_ingredientes", x => new { x.FichaId, x.Id });
                    table.ForeignKey(
                        name: "FK_ficha_ingredientes_fichas_FichaId",
                        column: x => x.FichaId,
                        principalSchema: "cardapio",
                        principalTable: "fichas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fichas_ShareToken",
                schema: "cardapio",
                table: "fichas",
                column: "ShareToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ficha_canais",
                schema: "cardapio");

            migrationBuilder.DropTable(
                name: "ficha_embalagens",
                schema: "cardapio");

            migrationBuilder.DropTable(
                name: "ficha_ingredientes",
                schema: "cardapio");

            migrationBuilder.DropTable(
                name: "fichas",
                schema: "cardapio");
        }
    }
}
