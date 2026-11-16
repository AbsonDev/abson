using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Precifica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pagamentos");

            migrationBuilder.EnsureSchema(
                name: "precificacao");

            migrationBuilder.CreateTable(
                name: "assinaturas",
                schema: "pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ValorMensal = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenciaGateway = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodoAtualFim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CanceladaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assinaturas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calculos",
                schema: "precificacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EditTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Nicho = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    QuantidadeProduzida = table.Column<int>(type: "integer", nullable: false),
                    PerdaDeMaterial = table.Column<int>(type: "integer", nullable: false),
                    MinutosDeTrabalho = table.Column<int>(type: "integer", nullable: false),
                    ValorHora = table.Column<long>(type: "bigint", nullable: false),
                    CustosFixosMensais = table.Column<long>(type: "bigint", nullable: false),
                    HorasProdutivasPorMes = table.Column<int>(type: "integer", nullable: false),
                    Imposto = table.Column<int>(type: "integer", nullable: false),
                    TaxaCartao = table.Column<int>(type: "integer", nullable: false),
                    TaxaFixaPorVenda = table.Column<long>(type: "bigint", nullable: false),
                    MargemDesejada = table.Column<int>(type: "integer", nullable: false),
                    Arredondamento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calculos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                schema: "pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Valor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenciaGateway = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PagamentoGatewayId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PagoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calculo_custos_extras",
                schema: "precificacao",
                columns: table => new
                {
                    CalculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Valor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calculo_custos_extras", x => new { x.CalculoId, x.Id });
                    table.ForeignKey(
                        name: "FK_calculo_custos_extras_calculos_CalculoId",
                        column: x => x.CalculoId,
                        principalSchema: "precificacao",
                        principalTable: "calculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calculo_materiais",
                schema: "precificacao",
                columns: table => new
                {
                    CalculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CustoUnitario = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calculo_materiais", x => new { x.CalculoId, x.Id });
                    table.ForeignKey(
                        name: "FK_calculo_materiais_calculos_CalculoId",
                        column: x => x.CalculoId,
                        principalSchema: "precificacao",
                        principalTable: "calculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assinaturas_Email",
                schema: "pagamentos",
                table: "assinaturas",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_assinaturas_ReferenciaGateway",
                schema: "pagamentos",
                table: "assinaturas",
                column: "ReferenciaGateway");

            migrationBuilder.CreateIndex(
                name: "IX_calculos_ShareToken",
                schema: "precificacao",
                table: "calculos",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_CalculoId_Status",
                schema: "pagamentos",
                table: "pedidos",
                columns: new[] { "CalculoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_ReferenciaGateway",
                schema: "pagamentos",
                table: "pedidos",
                column: "ReferenciaGateway");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assinaturas",
                schema: "pagamentos");

            migrationBuilder.DropTable(
                name: "calculo_custos_extras",
                schema: "precificacao");

            migrationBuilder.DropTable(
                name: "calculo_materiais",
                schema: "precificacao");

            migrationBuilder.DropTable(
                name: "pedidos",
                schema: "pagamentos");

            migrationBuilder.DropTable(
                name: "calculos",
                schema: "precificacao");
        }
    }
}
