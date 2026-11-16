using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Domain.Tests;

internal static class Fabrica
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 16, 12, 0, 0, TimeSpan.FromHours(-3));

    public static DadosDaFicha Dados(
        decimal custoIngredientes = 30m,
        decimal custoEmbalagem = 1m,
        int minutos = 60,
        decimal valorHora = 30m,
        decimal custoFixoMensal = 1000m,
        int porcoesMes = 1000,
        int rendimento = 10,
        decimal imposto = 6m,
        decimal cmv = 30m,
        IReadOnlyList<CanalDeVenda>? canais = null,
        Arredondamento arredondamento = Arredondamento.Nenhum)
    {
        return new DadosDaFicha
        {
            NomeDoPrato = "Frango grelhado com arroz e legumes",
            Estabelecimento = "Cozinha do Bairro",
            Categoria = "Prato principal",
            RendimentoEmPorcoes = rendimento,
            PorcoesVendidasPorMes = porcoesMes,
            Ingredientes =
            [
                new Ingrediente("Peito de frango", 1, "kg", Dinheiro.DeReais(custoIngredientes * 2m / 3m), Percentual.De(100m)),
                new Ingrediente("Arroz", 1, "kg", Dinheiro.DeReais(custoIngredientes / 6m), Percentual.De(100m)),
                new Ingrediente("Legumes", 1, "kg", Dinheiro.DeReais(custoIngredientes / 6m), Percentual.De(100m))
            ],
            Embalagens =
            [
                new Embalagem("Marmita", Dinheiro.DeReais(custoEmbalagem))
            ],
            MinutosDeProducao = minutos,
            ValorHoraMaoDeObra = Dinheiro.DeReais(valorHora),
            CustosFixosMensais = Dinheiro.DeReais(custoFixoMensal),
            Imposto = Percentual.De(imposto),
            CmvAlvo = Percentual.De(cmv),
            Canais = canais ??
            [
                new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero)
            ],
            Arredondamento = arredondamento
        };
    }

    public static FichaTecnica Ficha(DadosDaFicha? dados = null) =>
        FichaTecnica.Criar(
            dados ?? Dados(),
            Guid.NewGuid(),
            "share-token",
            new string('a', 64),
            Agora);
}
