using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;
using Precifica.Domain.Precificacao;

namespace Precifica.Domain.Tests;

internal static class Fabrica
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(-3));

    public static DadosDoCalculo Dados(
        decimal custoMaterial = 10m,
        int minutos = 60,
        decimal valorHora = 60m,
        decimal margem = 20m,
        decimal imposto = 0m,
        decimal taxaCartao = 0m,
        decimal perda = 0m,
        decimal custoFixoMensal = 0m,
        int horasProdutivas = 160,
        decimal taxaFixa = 0m,
        int quantidade = 1,
        Arredondamento arredondamento = Arredondamento.Nenhum)
    {
        return new DadosDoCalculo
        {
            Nicho = Nicho.Confeitaria,
            Titulo = "Bolo de chocolate",
            QuantidadeProduzida = quantidade,
            Materiais =
            [
                new ItemDeMaterial("Chocolate", 1, "kg", Dinheiro.DeReais(custoMaterial))
            ],
            PerdaDeMaterial = Percentual.De(perda),
            MinutosDeTrabalho = minutos,
            ValorHora = Dinheiro.DeReais(valorHora),
            CustosFixosMensais = Dinheiro.DeReais(custoFixoMensal),
            HorasProdutivasPorMes = horasProdutivas,
            Imposto = Percentual.De(imposto),
            TaxaCartao = Percentual.De(taxaCartao),
            TaxaFixaPorVenda = Dinheiro.DeReais(taxaFixa),
            MargemDesejada = Percentual.De(margem),
            Arredondamento = arredondamento
        };
    }

    public static Calculo Calculo(DadosDoCalculo? dados = null) =>
        Precifica.Domain.Precificacao.Calculo.Criar(
            dados ?? Dados(),
            Guid.NewGuid(),
            "share-token",
            new string('a', 64),
            Agora);

    public static Pedido Pedido(decimal valor = 29m) =>
        Precifica.Domain.Pagamentos.Pedido.CriarPdfAvulso(
            Guid.NewGuid(),
            "cliente@exemplo.com",
            Dinheiro.DeReais(valor),
            Agora);
}
