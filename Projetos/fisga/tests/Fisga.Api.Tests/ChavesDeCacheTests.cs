using Fisga.Application.Condicoes;
using Fisga.Infrastructure.Condicoes;

namespace Fisga.Api.Tests;

public sealed class ChavesDeCacheTests
{
    [Fact]
    public void Normaliza_termo_de_busca()
    {
        Assert.Equal("paraty", ChavesDeCache.NormalizarTermo("  PARATY  "));
        Assert.Equal("sao joao", ChavesDeCache.NormalizarTermo(" São   João "));
        Assert.True(ChavesDeCache.NormalizarTermo(new string('a', 200)).Length <= 80);
    }

    [Fact]
    public void Termo_vazio_continua_vazio()
    {
        Assert.Equal(string.Empty, ChavesDeCache.NormalizarTermo("   "));
    }

    [Fact]
    public void Chave_de_condicoes_arredonda_e_inclui_data()
    {
        Assert.Equal("condicoes:v1:-23.55:-45.13:2026-11-18",
            ChavesDeCache.Condicoes(-23.551, -45.134, new DateOnly(2026, 11, 18)));
    }

    [Fact]
    public void Chave_de_lua_usa_mesmo_arredondamento_e_data()
    {
        Assert.Equal("lua:v1:-23.55:-45.13:2026-11-18",
            ChavesDeCache.Lua(-23.551, -45.134, new DateOnly(2026, 11, 18)));
    }

    [Fact]
    public void Chave_de_busca_usa_termo_normalizado()
    {
        Assert.Equal("geocode:search:v1:sao joao",
            ChavesDeCache.BuscaDeLocal(" São   João "));
    }

    [Fact]
    public void Chave_de_reverso_usa_tres_casas()
    {
        Assert.Equal("geocode:reverse:v1:-23.551:-45.135",
            ChavesDeCache.Reverso(-23.5512, -45.1349));
    }

    [Fact]
    public void Ttl_completo_e_de_vinte_minutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(20), PoliticaDeCacheDeCondicoes.Ttl(Dados(temMare: true, comLua: true)));
    }

    [Fact]
    public void Ttl_parcial_e_de_dois_minutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), PoliticaDeCacheDeCondicoes.Ttl(Dados(temMare: false, comLua: true)));
        Assert.Equal(TimeSpan.FromMinutes(2), PoliticaDeCacheDeCondicoes.Ttl(Dados(temMare: true, comLua: false)));
    }

    [Fact]
    public void Ttl_sem_dados_e_de_trinta_segundos()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), PoliticaDeCacheDeCondicoes.Ttl(null));
    }

    private static DadosDeCondicoes Dados(bool temMare, bool comLua) => new(
        Horas: [],
        Dias: [],
        Lua: comLua ? new DadosDaLua(null, null, null, null, 0) : null,
        TemMare: temMare,
        Fonte: "teste");
}
