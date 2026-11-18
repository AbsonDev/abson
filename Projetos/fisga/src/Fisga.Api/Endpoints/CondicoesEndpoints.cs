using System.Diagnostics;
using Fisga.Api.Caching;
using Fisga.Api.Contracts;
using Fisga.Api.Observabilidade;
using Fisga.Application.Comparacao;
using Fisga.Application.Condicoes;
using Fisga.Application.Locais;
using Fisga.Domain.Pescarias;

namespace Fisga.Api.Endpoints;

public static class CondicoesEndpoints
{
    public static IEndpointRouteBuilder MapCondicoesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/condicoes", async (
                double? lat,
                double? lon,
                string? nome,
                string? cidade,
                string? estado,
                string? ambiente,
                ServicoDeCondicoes servico,
                CancellationToken ct) =>
            {
                if (lat is null || lon is null)
                {
                    return Results.BadRequest(
                        new ErroResponse("local_sem_coordenadas", "Informe a latitude e a longitude do local."));
                }

                var ambienteFinal = Enum.TryParse<AmbienteDePesca>(ambiente, true, out var valor)
                    ? valor
                    : AmbienteDePesca.Outro;

                var inicio = Stopwatch.GetTimestamp();
                var condicoes = await servico.ObterAsync(
                    lat.Value,
                    lon.Value,
                    nome,
                    cidade,
                    estado,
                    ambienteFinal,
                    ct);
                Metricas.CondicoesConsultadas(
                    ambienteFinal,
                    "condicoes",
                    Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

                return Results.Ok(condicoes);
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura")
            .WithMetadata(CachePolicy.Private(300, swr: 600));

        app.MapGet("/api/condicoes/comparar", async (
                double? lat,
                double? lon,
                string? nome,
                string? cidade,
                string? estado,
                string? ambiente,
                HttpContext http,
                ServicoDeCondicoes condicoesServico,
                ServicoDeComparacao comparacao,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();

                if (lat is null || lon is null)
                {
                    return Results.BadRequest(
                        new ErroResponse("local_sem_coordenadas", "Informe a latitude e a longitude do local."));
                }

                var ambienteFinal = Enum.TryParse<AmbienteDePesca>(ambiente, true, out var valor)
                    ? valor
                    : AmbienteDePesca.Outro;

                var inicio = Stopwatch.GetTimestamp();
                var condicoes = await condicoesServico.ObterAsync(
                    lat.Value,
                    lon.Value,
                    nome,
                    cidade,
                    estado,
                    ambienteFinal,
                    ct);
                Metricas.CondicoesConsultadas(
                    ambienteFinal,
                    "comparar",
                    Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

                var lua = Enum.TryParse<FaseDaLua>(condicoes.Solunar.Fase, true, out var faseLua)
                    ? faseLua
                    : FaseDaLua.NaoInformada;
                var mare = MapearMare(condicoes);
                var clima = MapearClima(condicoes);
                var periodo = MapearPeriodo(condicoes);

                var resultado = await comparacao.CompararAsync(usuarioId, lua, mare, clima, periodo, ct);
                return Results.Ok(resultado);
            })
            .RequireRateLimiting("leitura");

        app.MapGet("/api/locais/reverso", async (
                double? lat,
                double? lon,
                IGeocodificador geocodificador,
                CancellationToken ct) =>
            {
                if (lat is null || lon is null
                    || double.IsNaN(lat.Value) || double.IsNaN(lon.Value)
                    || lat is < -90 or > 90 || lon is < -180 or > 180)
                {
                    return Results.BadRequest(
                        new ErroResponse("coordenadas_invalidas", "Informe latitude e longitude válidas."));
                }

                var resultado = await geocodificador.ReversoAsync(lat.Value, lon.Value, ct);
                return resultado is null ? Results.NotFound() : Results.Ok(resultado);
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura")
            .WithMetadata(CachePolicy.Private(86400));

        app.MapGet("/api/locais/buscar", async (
                string? q,
                IGeocodificador geocodificador,
                CancellationToken ct) =>
            {
                var resultados = await geocodificador.BuscarAsync(q ?? string.Empty, ct);
                return Results.Ok(resultados);
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura")
            .WithMetadata(CachePolicy.Private(3600));

        var locais = app.MapGroup("/api/locais");

        locais.MapGet("", async (
                HttpContext http,
                ServicoDeLocais servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var lista = await servico.ListarAsync(usuarioId, ct);
                return Results.Ok(lista);
            })
            .RequireRateLimiting("leitura");

        locais.MapPost("", async (
                EntradaDeLocal entrada,
                HttpContext http,
                ServicoDeLocais servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var local = await servico.CriarAsync(entrada, usuarioId, ct);
                Metricas.LocalSalvo(entrada.Ambiente);
                return Results.Created($"/api/locais/{local.Id}", local);
            })
            .RequireRateLimiting("escrita");

        locais.MapPut("/{id:guid}", async (
                Guid id,
                EntradaDeLocal entrada,
                HttpContext http,
                ServicoDeLocais servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var local = await servico.AtualizarAsync(id, entrada, usuarioId, ct);
                return Results.Ok(local);
            })
            .RequireRateLimiting("escrita");

        locais.MapPost("/{id:guid}/padrao", async (
                Guid id,
                HttpContext http,
                ServicoDeLocais servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var local = await servico.DefinirPadraoAsync(id, usuarioId, ct);
                return Results.Ok(local);
            })
            .RequireRateLimiting("escrita");

        locais.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext http,
                ServicoDeLocais servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                await servico.ExcluirAsync(id, usuarioId, ct);
                return Results.NoContent();
            })
            .RequireRateLimiting("escrita");

        return app;
    }

    private static FaseDaMare MapearMare(CondicoesDto condicoes)
    {
        if (condicoes.Mare is null)
        {
            return FaseDaMare.NaoSeAplica;
        }

        if (condicoes.Mare.Estado == "subindo")
        {
            return FaseDaMare.Enchente;
        }

        if (condicoes.Mare.Estado == "descendo")
        {
            return FaseDaMare.Vazante;
        }

        return condicoes.Mare.ProximoEvento?.Tipo switch
        {
            "Cheia" => FaseDaMare.Cheia,
            "Baixa" => FaseDaMare.Seca,
            _ => FaseDaMare.NaoSeAplica
        };
    }

    private static Clima MapearClima(CondicoesDto condicoes)
    {
        if (condicoes.Tempo.ChuvaProbabilidade >= 60)
        {
            return Clima.Chuva;
        }

        if (condicoes.Tempo.VentoKmh >= 25)
        {
            return Clima.Vento;
        }

        if (condicoes.Tempo.ChuvaProbabilidade >= 30)
        {
            return Clima.Nublado;
        }

        return Clima.Sol;
    }

    private static PeriodoDoDia MapearPeriodo(CondicoesDto condicoes)
    {
        var hora = condicoes.AtualizadoEm.Hour;
        return hora switch
        {
            < 6 => PeriodoDoDia.Madrugada,
            < 12 => PeriodoDoDia.Manha,
            < 18 => PeriodoDoDia.Tarde,
            _ => PeriodoDoDia.Noite
        };
    }
}
