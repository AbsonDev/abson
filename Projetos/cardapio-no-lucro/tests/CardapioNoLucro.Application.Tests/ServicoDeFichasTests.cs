using CardapioNoLucro.Application.Catalogo;
using CardapioNoLucro.Application.Fichas;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Tests;

public sealed class ServicoDeFichasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 12, 0, 0, TimeSpan.FromHours(-3));

    private static EntradaDeFicha Entrada()
    {
        var catalogo = new CatalogoDeCanais();
        return new EntradaDeFicha
        {
            NomeDoPrato = "Frango grelhado com arroz e legumes",
            Estabelecimento = "Cozinha do Bairro",
            Categoria = "Prato principal",
            RendimentoEmPorcoes = 10,
            PorcoesVendidasPorMes = 1000,
            Ingredientes =
            [
                new EntradaIngrediente("Peito de frango", 1, "kg", 20m, 100m),
                new EntradaIngrediente("Arroz", 1, "kg", 5m, 100m),
                new EntradaIngrediente("Legumes", 1, "kg", 5m, 100m)
            ],
            Embalagens = [new EntradaEmbalagem("Marmita", 1m)],
            MinutosDeProducao = 60,
            ValorHoraMaoDeObra = 30m,
            CustosFixosMensais = 1000m,
            ImpostoPercentual = 6m,
            CmvAlvoPercentual = 30m,
            Canais = catalogo.ObterTodos()
                .Select(canal => new EntradaCanal(canal.Tipo, canal.ComissaoPercentual, canal.TaxaFixaPorPedido, canal.Ativo))
                .ToList(),
            Arredondamento = Arredondamento.Nenhum
        };
    }

    [Fact]
    public void Simula_e_entrega_preco_por_canal()
    {
        var servico = new ServicoDeFichas(
            new FichaRepositorioFake(),
            new CatalogoDeCanais(),
            new RelogioFixo(Agora),
            new DispatcherFake());

        var resultado = servico.Simular(Entrada());

        Assert.Equal(800, (int)(resultado.CustoTotalPorcao * 100));
        Assert.Equal(5, resultado.Canais.Count);
        Assert.Contains(resultado.Canais, canal => canal.Tipo == "IFood" && canal.Nome == "iFood");
        Assert.All(resultado.Canais, canal => Assert.True(canal.PrecoSugerido > 0));
    }

    [Fact]
    public async Task Cria_ficha_e_publica_evento()
    {
        var repositorio = new FichaRepositorioFake();
        var eventos = new DispatcherFake();
        var servico = new ServicoDeFichas(repositorio, new CatalogoDeCanais(), new RelogioFixo(Agora), eventos);

        var salva = await servico.CriarAsync(Entrada());

        Assert.Single(repositorio.Fichas);
        Assert.Single(eventos.Publicados);
        Assert.Equal(32, salva.ShareToken.Length);
        Assert.Equal(48, salva.EditToken.Length);
    }

    [Fact]
    public async Task Atualiza_com_token_correto_e_rejeita_token_errado()
    {
        var repositorio = new FichaRepositorioFake();
        var servico = new ServicoDeFichas(repositorio, new CatalogoDeCanais(), new RelogioFixo(Agora), new DispatcherFake());
        var salva = await servico.CriarAsync(Entrada());

        var atualizada = await servico.AtualizarAsync(salva.Id, salva.EditToken, Entrada());
        Assert.NotNull(atualizada);

        await Assert.ThrowsAsync<Abstractions.AcessoNegadoException>(() =>
            servico.AtualizarAsync(salva.Id, "token-errado", Entrada()));
    }

    [Fact]
    public async Task Obter_publico_devolve_a_ficha_sem_token_de_edicao()
    {
        var repositorio = new FichaRepositorioFake();
        var servico = new ServicoDeFichas(repositorio, new CatalogoDeCanais(), new RelogioFixo(Agora), new DispatcherFake());
        var salva = await servico.CriarAsync(Entrada());

        var publica = await servico.ObterPublicoAsync(salva.ShareToken);

        Assert.NotNull(publica);
        Assert.Equal("Frango grelhado com arroz e legumes", publica.NomeDoPrato);
        Assert.NotEqual(Guid.Empty, publica.Id);
    }
}
