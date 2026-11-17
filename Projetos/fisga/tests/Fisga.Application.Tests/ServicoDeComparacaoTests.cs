using Fisga.Application.Comparacao;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Tests;

public sealed class ServicoDeComparacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 12, 0, 0, TimeSpan.FromHours(-3));
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static Pescaria Criar(
        FaseDaLua lua,
        FaseDaMare mare,
        Clima clima,
        PeriodoDoDia periodo,
        DateOnly data,
        int capturas = 1)
    {
        var dados = new DadosDaPescaria
        {
            Local = "Rio Sergipe",
            Cidade = "Aracaju",
            Estado = "SE",
            Ambiente = AmbienteDePesca.Mar,
            Data = data,
            Periodo = periodo,
            Mare = mare,
            Lua = lua,
            Clima = clima,
            ClarezaDaAgua = ClarezaDaAgua.Turva,
            Capturas = Enumerable.Range(0, capturas)
                .Select(_ => new DadosDaCaptura(
                    null,
                    "Tucunaré",
                    2.4m,
                    48m,
                    "Popper",
                    true,
                    null,
                    null))
                .ToList()
        };

        return Pescaria.Criar(
            dados,
            Guid.NewGuid(),
            UsuarioId,
            Guid.NewGuid().ToString("N"),
            Agora);
    }

    [Fact]
    public async Task Encontra_o_dia_mais_parecido_e_explica_os_fatores()
    {
        var repositorio = new PescariaRepositorioFake();
        var melhor = Criar(
            FaseDaLua.Crescente,
            FaseDaMare.Enchente,
            Clima.Sol,
            PeriodoDoDia.Tarde,
            new DateOnly(2026, 10, 12),
            capturas: 3);

        repositorio.Pescarias.Add(melhor);
        repositorio.Pescarias.Add(Criar(
            FaseDaLua.Cheia,
            FaseDaMare.Vazante,
            Clima.Chuva,
            PeriodoDoDia.Manha,
            new DateOnly(2026, 9, 2)));
        repositorio.Pescarias.Add(Criar(
            FaseDaLua.Crescente,
            FaseDaMare.Vazante,
            Clima.Nublado,
            PeriodoDoDia.Manha,
            new DateOnly(2026, 8, 15)));

        var servico = new ServicoDeComparacao(repositorio);
        var resultado = await servico.CompararAsync(
            UsuarioId,
            FaseDaLua.Crescente,
            FaseDaMare.Enchente,
            Clima.Sol,
            PeriodoDoDia.Tarde);

        Assert.True(resultado.TemDados);
        Assert.Contains("Tucunaré", resultado.Titulo);
        Assert.Equal(melhor.ShareToken, resultado.ShareToken);
        Assert.Contains(resultado.Fatores, fator => fator.Contains("Lua crescente"));
        Assert.Contains(resultado.Fatores, fator => fator.Contains("enchente", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, resultado.PescariasConsideradas);
    }

    [Fact]
    public async Task Conta_quantas_pescarias_faltam_para_comparar()
    {
        var repositorio = new PescariaRepositorioFake();
        repositorio.Pescarias.Add(Criar(
            FaseDaLua.Crescente,
            FaseDaMare.Enchente,
            Clima.Sol,
            PeriodoDoDia.Tarde,
            new DateOnly(2026, 10, 12)));

        var servico = new ServicoDeComparacao(repositorio);
        var resultado = await servico.CompararAsync(
            UsuarioId,
            FaseDaLua.Crescente,
            FaseDaMare.Enchente,
            Clima.Sol,
            PeriodoDoDia.Tarde);

        Assert.False(resultado.TemDados);
        Assert.Contains("mais 2", resultado.Detalhe);
        Assert.Equal(1, resultado.PescariasConsideradas);
    }

    [Fact]
    public async Task Sem_dia_parecido_nao_inventa_padrao()
    {
        var repositorio = new PescariaRepositorioFake();
        repositorio.Pescarias.Add(Criar(
            FaseDaLua.Nova,
            FaseDaMare.Seca,
            Clima.Chuva,
            PeriodoDoDia.Madrugada,
            new DateOnly(2026, 10, 1)));
        repositorio.Pescarias.Add(Criar(
            FaseDaLua.Cheia,
            FaseDaMare.Cheia,
            Clima.Vento,
            PeriodoDoDia.Noite,
            new DateOnly(2026, 10, 2)));
        repositorio.Pescarias.Add(Criar(
            FaseDaLua.Minguante,
            FaseDaMare.NaoSeAplica,
            Clima.Nublado,
            PeriodoDoDia.Manha,
            new DateOnly(2026, 10, 3)));

        var servico = new ServicoDeComparacao(repositorio);
        var resultado = await servico.CompararAsync(
            UsuarioId,
            FaseDaLua.Crescente,
            FaseDaMare.Enchente,
            Clima.Sol,
            PeriodoDoDia.Tarde);

        Assert.False(resultado.TemDados);
        Assert.Equal("Nenhum dia parecido ainda", resultado.Titulo);
    }
}
