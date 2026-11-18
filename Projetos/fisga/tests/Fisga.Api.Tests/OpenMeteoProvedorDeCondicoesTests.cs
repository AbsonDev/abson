using System.Net;
using System.Text;
using System.Text.Json;
using Fisga.Application.Condicoes;
using Fisga.Infrastructure.Condicoes;
using Microsoft.Extensions.Logging;

namespace Fisga.Api.Tests;

public sealed class OpenMeteoProvedorDeCondicoesTests
{
    private const string ClimaJson =
        """
        {"hourly":{"time":["2026-11-17T15:00"],"temperature_2m":[27.5],"apparent_temperature":[29.0],
        "precipitation_probability":[10],"pressure_msl":[1012.0],"wind_speed_10m":[12.0],
        "wind_gusts_10m":[20.0],"wind_direction_10m":[90],"cloud_cover":[30]},
        "daily":{"time":["2026-11-17"],"sunrise":["2026-11-17T05:00"],"sunset":["2026-11-17T17:40"]}}
        """;

    [Fact]
    public async Task Clima_tenta_de_novo_quando_open_meteo_responde_200_sem_json()
    {
        var handler = new HandlerFalso(
            Texto("Upstream request timeout"),
            Json(ClimaJson));
        var logger = new LoggerCapturador();
        var provedor = Criar(handler, logger);

        var dados = await provedor.ObterAsync(-10.911, -37.071, new DateOnly(2026, 11, 17));

        Assert.NotNull(dados);
        Assert.Single(dados!.Horas);
        Assert.Equal(27.5, dados.Horas[0].TemperaturaC);
        Assert.Equal(2, handler.ChamadasAoClima);
        Assert.DoesNotContain(logger.Registros, registro => registro.Nivel == LogLevel.Warning);
    }

    [Fact]
    public async Task Clima_registra_aviso_com_trecho_do_corpo_quando_resposta_segue_sem_json()
    {
        var handler = new HandlerFalso(
            Texto("Upstream request timeout"),
            Texto("Upstream request timeout"));
        var logger = new LoggerCapturador();
        var provedor = Criar(handler, logger);

        var dados = await provedor.ObterAsync(-10.911, -37.071, new DateOnly(2026, 11, 17));

        Assert.Null(dados);
        Assert.Equal(2, handler.ChamadasAoClima);
        var aviso = Assert.Single(logger.Registros, registro => registro.Nivel == LogLevel.Warning);
        Assert.Equal("Falha ao buscar clima em Open-Meteo", aviso.Mensagem);
        var excecao = Assert.IsType<JsonException>(aviso.Excecao);
        Assert.Contains("Upstream request timeout", excecao.Message);
        Assert.Contains("text/plain", excecao.Message);
        Assert.Contains("200", excecao.Message);
    }

    private static OpenMeteoProvedorDeCondicoes Criar(HandlerFalso handler, LoggerCapturador logger) =>
        new(new HttpClient(handler), new CacheEmMemoriaDeCondicoes(), new CondicoesOptions(), logger);

    private static HttpResponseMessage Texto(string corpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "text/plain") };

    private static HttpResponseMessage Json(string corpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private sealed class HandlerFalso(params HttpResponseMessage[] respostasDoClima) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _clima = new(respostasDoClima);

        public int ChamadasAoClima { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "api.open-meteo.com")
            {
                ChamadasAoClima++;
                var resposta = _clima.Dequeue();
                resposta.RequestMessage = request;
                return Task.FromResult(resposta);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }

    private sealed record Registro(LogLevel Nivel, string Mensagem, Exception? Excecao);

    private sealed class LoggerCapturador : ILogger<OpenMeteoProvedorDeCondicoes>
    {
        public List<Registro> Registros { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Registros.Add(new Registro(logLevel, formatter(state, exception), exception));
    }
}
