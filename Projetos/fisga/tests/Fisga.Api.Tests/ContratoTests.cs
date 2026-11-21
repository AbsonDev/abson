using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Caching;
using Fisga.Api.Contracts;
using Fisga.Api.OpenApi;
using Fisga.Api.RateLimiting;
using Fisga.Application.Locais;
using Fisga.Application.Pontos;
using Fisga.Application.Trilhas;

namespace Fisga.Api.Tests;

// Invariantes do contrato OpenAPI por operação. O documento é o que a API
// publica em /openapi/v1.json (disponível porque a factory de teste usa
// Development); a expectativa de segurança/cache/rate limit vem da metadata
// real de cada endpoint, para que o contrato não divirja do comportamento.
[Collection("api")]
public sealed class ContratoTests
{
    // Operações sem política de rate limit nesta fase; o teste 4 exige que a
    // ausência seja explícita, não que o contrato invente um número.
    private static readonly HashSet<(string Metodo, string Rota)> SemRateLimit =
    [
        ("GET", "/health"),
        ("GET", "/ready"),
        ("POST", "/api/newsletter/enviar"),
        ("GET", "/api/newsletter/preview")
    ];

    // As duas rotas administrativas são AllowAnonymous no código, mas exigem o
    // header X-Admin-Token por checagem manual; o contrato precisa dizer isso.
    private static readonly HashSet<(string Metodo, string Rota)> ComTokenAdmin =
    [
        ("POST", "/api/newsletter/enviar"),
        ("GET", "/api/newsletter/preview")
    ];

    // Rotas anônimas por decisão de produto: leitura pública, login, recuperação,
    // descadastro e saúde. A invariante é a intenção, não a metadata do endpoint —
    // derivar de IAllowAnonymous faria o teste concordar com qualquer mudança.
    private static readonly HashSet<(string Metodo, string Rota)> RotasAnonimasPorDecisao =
    [
        ("GET", "/api/auth/confirmar"),
        ("POST", "/api/auth/confirmar-email"),
        ("POST", "/api/auth/entrar"),
        ("POST", "/api/auth/esqueci-senha"),
        ("GET", "/api/auth/google/concluir"),
        ("GET", "/api/auth/google/disponivel"),
        ("GET", "/api/auth/google/iniciar"),
        ("GET", "/api/auth/me"),
        ("POST", "/api/auth/redefinir-senha"),
        ("POST", "/api/auth/registrar"),
        ("POST", "/api/auth/sair"),
        ("POST", "/api/auth/solicitar"),
        ("GET", "/api/catalogo"),
        ("GET", "/api/condicoes"),
        ("GET", "/api/legal/versoes"),
        ("GET", "/api/locais/buscar"),
        ("GET", "/api/locais/reverso"),
        ("GET", "/api/newsletter/descadastrar"),
        ("GET", "/api/pescarias/{shareToken}"),
        ("GET", "/api/pescarias/{shareToken}/pdf"),
        ("GET", "/api/pescarias/{shareToken}/permissao"),
        ("GET", "/health"),
        ("GET", "/ready")
    ];

    public static TheoryData<string> Dominios => new(Tags.Todas);

    private static Task<JsonDocument>? _documento;

    private readonly ApiTestFixture _fixture;

    public ContratoTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TodaOperacaoTemOperationIdDoMapa()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(54, operacoes.Count);

        var operationIds = new List<string>();
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var esperado = MapaDeOperacoes.Para(metodo, rota);
            Assert.True(
                esperado is not null,
                $"Operação sem operationId no mapa: {metodo} {rota}");

            Assert.True(
                operacao.TryGetProperty("operationId", out var operationId),
                $"Operação sem operationId no contrato: {metodo} {rota}");
            Assert.Equal(esperado, operationId.GetString());
            Assert.False(
                string.IsNullOrWhiteSpace(operationId.GetString()),
                $"operationId vazio: {metodo} {rota}");

            operationIds.Add(operationId.GetString()!);
        }

        Assert.Equal(operacoes.Count, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(operacoes.Count, MapaDeOperacoes.Todos.Count);
    }

    [Fact]
    public async Task ResumosSaoDeNegocio()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(54, operacoes.Count);

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();

        foreach (var (metodo, rota, operacao) in operacoes)
        {
            if (!operacao.TryGetProperty("summary", out var resumo))
            {
                violacoes.Add($"Operação sem summary: {metodo} {rota}");
                continue;
            }

            var textoDoResumo = resumo.GetString();
            if (string.IsNullOrWhiteSpace(textoDoResumo))
            {
                violacoes.Add($"summary vazio: {metodo} {rota}");
                continue;
            }

            // Resumo é propósito de negócio, não nome de handler ou assinatura
            // de código copiada para o contrato.
            foreach (var jargao in new[] { "Fisga.Api", "{", "(", "Endpoints" })
            {
                if (textoDoResumo.Contains(jargao, StringComparison.Ordinal))
                {
                    violacoes.Add($"Resumo com jargão de código \"{jargao}\": {metodo} {rota}");
                }
            }
        }

        Assert.Empty(violacoes);
    }

    [Fact]
    public async Task DocumentoTemIdentidadePublica()
    {
        var raiz = (await LoadAsync()).RootElement;
        var info = raiz.GetProperty("info");

        Assert.Equal("API do Fisga", info.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("version").GetString()));

        var descricao = info.GetProperty("description").GetString() ?? string.Empty;
        Assert.Contains("consentimento_pendente", descricao);
        Assert.Contains("origem_invalida", descricao);
        Assert.Contains("Retry-After", descricao);

        Assert.Equal("Fisga", info.GetProperty("contact").GetProperty("name").GetString());
        Assert.Equal(
            "https://fisga.abson.tech",
            info.GetProperty("contact").GetProperty("url").GetString());
        Assert.Equal("Proprietária", info.GetProperty("license").GetProperty("name").GetString());
        Assert.False(info.GetProperty("license").TryGetProperty("url", out _));
        Assert.False(info.GetProperty("license").TryGetProperty("identifier", out _));
        Assert.False(info.GetProperty("contact").TryGetProperty("email", out _));

        var servidores = raiz.GetProperty("servers").EnumerateArray().ToList();
        Assert.Equal(2, servidores.Count);
        Assert.Equal("https://api.fisga.abson.tech", servidores[0].GetProperty("url").GetString());
        Assert.Equal("Produção", servidores[0].GetProperty("description").GetString());
        Assert.Equal("http://localhost:5184", servidores[1].GetProperty("url").GetString());
        Assert.Equal("Local", servidores[1].GetProperty("description").GetString());
    }

    [Fact]
    public async Task TagsTemDescricao()
    {
        var raiz = (await LoadAsync()).RootElement;
        var tags = raiz.GetProperty("tags").EnumerateArray().ToList();

        Assert.Equal(Tags.Todas.Count, tags.Count);
        Assert.Equal(
            Tags.Todas.OrderBy(nome => nome, StringComparer.Ordinal),
            tags.Select(tag => tag.GetProperty("name").GetString())
                .OrderBy(nome => nome, StringComparer.Ordinal));

        foreach (var tag in tags)
        {
            var nome = tag.GetProperty("name").GetString()!;
            Assert.True(
                tag.TryGetProperty("description", out var descricao),
                $"Tag sem descrição: {nome}");
            Assert.Equal(Tags.Descricoes[nome], descricao.GetString());
        }
    }

    [Fact]
    public async Task ComportamentosQueSurpreendemEstaoDocumentados()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        string Descricao(string metodo, string rota)
        {
            var encontrada = operacoes.SingleOrDefault(
                operacao => operacao.Metodo == metodo && operacao.Rota == rota);
            Assert.True(
                encontrada.Operacao.ValueKind != JsonValueKind.Undefined,
                $"Operação ausente no contrato: {metodo} {rota}");

            return encontrada.Operacao.GetProperty("description").GetString() ?? string.Empty;
        }

        // 1. 404 proposital das rotas administrativas.
        foreach (var rota in new[] { "/api/newsletter/enviar", "/api/newsletter/preview" })
        {
            var metodo = rota.EndsWith("enviar", StringComparison.Ordinal) ? "POST" : "GET";
            var descricao = Descricao(metodo, rota);
            Assert.Contains("proposital", descricao, StringComparison.Ordinal);
            Assert.Contains("404 sem corpo", descricao, StringComparison.Ordinal);
        }

        // 2. GET de auth com efeito colateral e os três destinos do 302.
        foreach (var rota in new[] { "/api/auth/confirmar", "/api/auth/google/concluir" })
        {
            var descricao = Descricao("GET", rota);
            Assert.Contains("efeito colateral", descricao, StringComparison.Ordinal);
            Assert.Contains("/boas-vindas?novo=1", descricao, StringComparison.Ordinal);
            Assert.Contains("estado=ok", descricao, StringComparison.Ordinal);
            Assert.Contains("estado=<codigo>", descricao, StringComparison.Ordinal);
        }

        Assert.Contains(
            "estado=google_conectado",
            Descricao("GET", "/api/auth/google/conectar/concluir"),
            StringComparison.Ordinal);

        // 3. 200 com autenticado:false em /api/auth/me, como exemplo da resposta.
        var me = operacoes.SingleOrDefault(operacao =>
            operacao.Metodo == "GET" && operacao.Rota == "/api/auth/me");
        Assert.True(
            me.Operacao.ValueKind != JsonValueKind.Undefined,
            "Operação ausente no contrato: GET /api/auth/me");

        var exemplo = me.Operacao.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("examples").GetProperty("semSessao")
            .GetProperty("value");

        Assert.False(exemplo.GetProperty("autenticado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, exemplo.GetProperty("usuario").ValueKind);

        // O exemplo tem que ter as mesmas chaves do schema publicado, senão vira
        // documentação velha na primeira propriedade nova do MeResponse.
        var propriedades = documento.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("MeResponse")
            .GetProperty("properties").EnumerateObject().Select(propriedade => propriedade.Name)
            .OrderBy(nome => nome, StringComparer.Ordinal);
        var chavesDoExemplo = exemplo.EnumerateObject().Select(propriedade => propriedade.Name)
            .OrderBy(nome => nome, StringComparer.Ordinal);
        Assert.Equal(propriedades, chavesDoExemplo);
    }

    [Fact]
    public async Task TagsSaoDeDominio()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(13, Tags.Todas.Count);
        Assert.Equal(13, Tags.Todas.Distinct(StringComparer.Ordinal).Count());

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();
        var tagsUsadas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            if (!operacao.TryGetProperty("tags", out var tags))
            {
                violacoes.Add($"Operação sem tags: {metodo} {rota}");
                continue;
            }

            if (tags.ValueKind != JsonValueKind.Array)
            {
                violacoes.Add($"Operação deve ter exatamente uma tag de domínio: {metodo} {rota}");
                continue;
            }

            if (tags.GetArrayLength() != 1)
            {
                violacoes.Add($"Operação deve ter exatamente uma tag de domínio: {metodo} {rota}");
            }

            foreach (var tag in tags.EnumerateArray())
            {
                var nome = tag.GetString();
                if (string.IsNullOrWhiteSpace(nome))
                {
                    violacoes.Add($"Tag vazia: {metodo} {rota}");
                    continue;
                }

                if (nome == "Fisga.Api")
                {
                    violacoes.Add($"Tag fora do domínio: {nome} em {metodo} {rota}");
                    continue;
                }

                if (!Tags.Todas.Contains(nome, StringComparer.Ordinal))
                {
                    violacoes.Add($"Tag fora do domínio: {nome} em {metodo} {rota}");
                    continue;
                }

                tagsUsadas.Add(nome);
            }
        }

        // Nenhuma das 13 pode ficar órfã: toda área do domínio tem operação.
        foreach (var tag in Tags.Todas)
        {
            if (!tagsUsadas.Contains(tag))
            {
                violacoes.Add($"Tag órfã sem operação: {tag}");
            }
        }

        Assert.Empty(violacoes);
    }

    [Fact]
    public async Task SegurancaExpressaPorOperacao()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        // A asserção de contagem impede que os invariantes passem vazios caso
        // o documento publicado perca "paths" mas mantenha os schemes.
        Assert.Equal(54, operacoes.Count);

        foreach (var (metodo, rota, operacao) in operacoes)
        {
            // Chave presente mesmo quando o array é vazio: segurança é decisão
            // explícita por operação, nunca omissão.
            Assert.True(
                operacao.TryGetProperty("security", out var security),
                $"Operação sem security: {metodo} {rota}");
            Assert.Equal(JsonValueKind.Array, security.ValueKind);

            var esquemas = Esquemas(security);

            if (ComTokenAdmin.Contains((metodo, rota)))
            {
                Assert.Contains("adminToken", esquemas);
                Assert.DoesNotContain("cookieAuth", esquemas);
                continue;
            }

            var anonima = RotasAnonimasPorDecisao.Contains((metodo, rota));
            if (anonima)
            {
                Assert.Empty(esquemas);
            }
            else
            {
                Assert.Contains("cookieAuth", esquemas);
            }
        }

        Assert.Equal(23, RotasAnonimasPorDecisao.Count);
        Assert.Equal(2, ComTokenAdmin.Count);

        var schemes = documento.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(
            schemes.TryGetProperty("cookieAuth", out var cookieAuth),
            "components.securitySchemes.cookieAuth ausente");
        Assert.Equal("apiKey", cookieAuth.GetProperty("type").GetString());
        Assert.Equal("cookie", cookieAuth.GetProperty("in").GetString());
        Assert.Equal("fisga_auth", cookieAuth.GetProperty("name").GetString());

        Assert.True(
            schemes.TryGetProperty("adminToken", out var adminToken),
            "components.securitySchemes.adminToken ausente");
        Assert.Equal("apiKey", adminToken.GetProperty("type").GetString());
        Assert.Equal("header", adminToken.GetProperty("in").GetString());
        Assert.Equal("X-Admin-Token", adminToken.GetProperty("name").GetString());
    }

    [Fact]
    public async Task RateLimitECacheRefletemMetadata()
    {
        var documento = await LoadAsync();
        var configuracao = _fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var operacoes = Operacoes(documento);
        var comRateLimit = 0;

        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var endpoint = Localizar(metodo, rota);

            // x-cache é o Cache-Control exato do 2xx; sem política, o
            // default-deny do middleware responde "private, no-store".
            var cacheEsperado = endpoint.Metadata.GetMetadata<CachePolicy>()?.HeaderValue
                ?? CachePolicy.NoStore.HeaderValue;
            Assert.True(
                operacao.TryGetProperty("x-cache", out var cache),
                $"Operação sem x-cache: {metodo} {rota}");
            Assert.Equal(JsonValueKind.String, cache.ValueKind);
            Assert.Equal(cacheEsperado, cache.GetString());

            var politica = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();

            if (SemRateLimit.Contains((metodo, rota)))
            {
                Assert.True(
                    politica is null,
                    $"Operação isenta ganhou política de rate limit: {metodo} {rota}");
                Assert.False(
                    operacao.TryGetProperty("x-rate-limit", out _),
                    $"Operação isenta com x-rate-limit: {metodo} {rota}");
                continue;
            }

            comRateLimit++;

            Assert.True(
                politica is not null,
                $"Operação sem política de rate limit: {metodo} {rota}");
            var nomeDaPolitica = politica!.PolicyName;
            Assert.NotNull(nomeDaPolitica);

            Assert.True(
                operacao.TryGetProperty("x-rate-limit", out var rateLimit),
                $"Operação sem x-rate-limit: {metodo} {rota}");
            Assert.Equal(nomeDaPolitica, rateLimit.GetProperty("policy").GetString());

            Assert.Equal(
                PoliticasDeTaxa.LimiteDe(configuracao, nomeDaPolitica),
                rateLimit.GetProperty("requests").GetInt32());
            Assert.Equal(
                PoliticasDeTaxa.JanelaEmSegundos,
                rateLimit.GetProperty("windowSeconds").GetInt32());
        }

        Assert.Equal(operacoes.Count - SemRateLimit.Count, comRateLimit);
    }

    [Theory]
    [MemberData(nameof(Dominios))]
    public async Task RespostasDeclaradasSaoAsReais(string dominio)
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento)
            .Where(operacao => Tag(operacao.Operacao) == dominio)
            .ToList();

        Assert.NotEmpty(operacoes);

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var chave = (metodo, rota);
            if (!RespostasEsperadas.PorOperacao.TryGetValue(chave, out var esperados))
            {
                violacoes.Add($"Operação sem status esperado: {metodo} {rota}");
                continue;
            }

            var declarados = operacao.GetProperty("responses").EnumerateObject()
                .Select(resposta => resposta.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var listaEsperada = esperados
                .Select(status => status.ToString(CultureInfo.InvariantCulture))
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (!listaEsperada.SequenceEqual(declarados))
            {
                violacoes.Add(
                    $"{metodo} {rota}: esperado [{string.Join(", ", listaEsperada)}], " +
                    $"declarado [{string.Join(", ", declarados)}]");
            }
        }

        Assert.Empty(violacoes);
    }

    [Fact]
    public void TabelaDeRespostasCobreAs54Operacoes()
    {
        Assert.Equal(54, MapaDeOperacoes.Todos.Count);
        Assert.Equal(
            MapaDeOperacoes.Todos.Keys.Order(StringComparer.Ordinal),
            RespostasEsperadas.PorOperacao.Keys
                .Select(chave => $"{chave.Metodo} {chave.Rota.TrimStart('/')}")
                .Order(StringComparer.Ordinal));
    }

    // A política de rate limit é publicada por operação em x-rate-limit; um 429
    // que pode acontecer e não está declarado é contrato mentindo, e um 429
    // declarado sem política é ruído. O lado do x-rate-limit vem do documento
    // publicado, não da tabela: se a metadata do endpoint ou a tabela derivarem,
    // este teste acusa.
    [Fact]
    public async Task RateLimitEContratoDeclaram429Juntos()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(54, operacoes.Count);

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();
        var comRateLimit = 0;
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var comPolitica = operacao.TryGetProperty("x-rate-limit", out _);
            var declara429 = RespostasEsperadas.PorOperacao.TryGetValue((metodo, rota), out var esperados)
                && esperados.Contains(429);

            if (comPolitica)
            {
                comRateLimit++;
            }

            if (comPolitica && !declara429)
            {
                violacoes.Add($"{metodo} {rota}: publica x-rate-limit e não declara 429");
            }
            else if (!comPolitica && declara429)
            {
                violacoes.Add($"{metodo} {rota}: declara 429 sem política de rate limit");
            }
        }

        Assert.Empty(violacoes);
        Assert.Equal(50, comRateLimit);
        Assert.Equal(
            50,
            RespostasEsperadas.PorOperacao.Values.Count(esperados => esperados.Contains(429)));
    }

    [Theory]
    [MemberData(nameof(Dominios))]
    public async Task ErrosUsamUmUnicoSchema(string dominio)
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento)
            .Where(operacao => Tag(operacao.Operacao) == dominio)
            .ToList();

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var chave = (metodo, rota);
            foreach (var resposta in operacao.GetProperty("responses").EnumerateObject())
            {
                if (!int.TryParse(
                        resposta.Name,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var status))
                {
                    violacoes.Add($"{metodo} {rota}: resposta não numérica \"{resposta.Name}\"");
                    continue;
                }

                var objeto = resposta.Value;
                var temConteudo = objeto.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.Object
                    && content.EnumerateObject().Any();

                if (!RespostasEsperadas.TemCorpo(chave, status))
                {
                    if (temConteudo)
                    {
                        violacoes.Add($"{metodo} {rota} {status}: resposta sem corpo declarou content");
                    }

                    if (status == 429)
                    {
                        var headers = objeto.TryGetProperty("headers", out var h) ? h : default;
                        if (headers.ValueKind != JsonValueKind.Object
                            || !headers.TryGetProperty("Retry-After", out _))
                        {
                            violacoes.Add($"{metodo} {rota}: 429 sem header Retry-After");
                        }
                    }

                    continue;
                }

                if (status == 201 || status == 202)
                {
                    var headers = objeto.TryGetProperty("headers", out var h) ? h : default;
                    if (headers.ValueKind != JsonValueKind.Object
                        || !headers.TryGetProperty("Location", out _))
                    {
                        violacoes.Add($"{metodo} {rota}: {status} sem header Location");
                    }
                }

                if (status < 400)
                {
                    continue;
                }

                if (!temConteudo)
                {
                    violacoes.Add($"{metodo} {rota} {status}: resposta com corpo sem content");
                    continue;
                }

                foreach (var tipo in new[] { RespostasPadrao.TipoProblema, RespostasPadrao.TipoJson })
                {
                    if (!content.TryGetProperty(tipo, out var midia)
                        || !midia.TryGetProperty("schema", out var schema)
                        || !schema.TryGetProperty("$ref", out var referencia)
                        || referencia.GetString() != "#/components/schemas/ErroResponse")
                    {
                        violacoes.Add($"{metodo} {rota} {status}: sem schema ErroResponse em {tipo}");
                    }
                }

                if (!objeto.TryGetProperty("description", out var descricao)
                    || descricao.GetString() != RespostasPadrao.DescricaoComErro(status))
                {
                    violacoes.Add($"{metodo} {rota} {status}: descrição fora do catálogo");
                }
            }
        }

        Assert.Empty(violacoes);
    }

    [Fact]
    public async Task UploadsDescrevemMultipart()
    {
        var documento = await LoadAsync();

        foreach (var (metodo, rota) in new[]
                 {
                     ("POST", "/api/midia"),
                     ("POST", "/api/especies/sugerir")
                 })
        {
            var operacao = Operacoes(documento).Single(o => o.Metodo == metodo && o.Rota == rota).Operacao;
            var multipart = operacao.GetProperty("requestBody")
                .GetProperty("content")
                .GetProperty("multipart/form-data");
            var referencia = multipart.GetProperty("schema").GetProperty("$ref").GetString()!;
            var nome = referencia["#/components/schemas/".Length..];
            var schema = documento.RootElement
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty(nome);

            Assert.True(schema.GetProperty("properties").TryGetProperty("arquivo", out var arquivo));
            Assert.Contains("arquivo", schema.GetProperty("required").EnumerateArray().Select(v => v.GetString()));

            var arquivoRef = arquivo.GetProperty("$ref").GetString()!;
            var arquivoSchema = documento.RootElement
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty(arquivoRef["#/components/schemas/".Length..]);
            Assert.Equal("string", arquivoSchema.GetProperty("type").GetString());
            Assert.Equal("binary", arquivoSchema.GetProperty("format").GetString());
        }
    }

    // Saúde e prontidão respondem JSON de verdade: Produces(200, null, …) compila
    // mas o gerador descarta o content quando o tipo é nulo, e o contrato passaria
    // a dizer "200 sem corpo". O $ref prova que o schema publicado é o record que o
    // handler devolve, e o corpo real é lido para garantir que `status` e `version`
    // são as propriedades que a operação serve — um rename no record muda schema e
    // corpo juntos, e este teste acusa a divergência em vez de benzer o contrato.
    [Fact]
    public async Task SaudeDeclaraCorpoJson()
    {
        var documento = await LoadAsync();
        var schemas = documento.RootElement.GetProperty("components").GetProperty("schemas");
        using var cliente = _fixture.Factory.CreateClient();

        foreach (var (metodo, rota, nome, propriedades) in new[]
                 {
                     ("GET", "/health", nameof(SaudeResponse), new[] { "status", "version" }),
                     ("GET", "/ready", nameof(ProntidaoResponse), new[] { "status" })
                 })
        {
            var operacao = Operacoes(documento).Single(o => o.Metodo == metodo && o.Rota == rota).Operacao;
            var resposta = operacao.GetProperty("responses").GetProperty("200");
            var json = resposta.GetProperty("content").GetProperty(RespostasPadrao.TipoJson);

            var referencia = json.GetProperty("schema").GetProperty("$ref").GetString()!;
            Assert.Equal($"#/components/schemas/{nome}", referencia);

            var schema = schemas.GetProperty(referencia["#/components/schemas/".Length..]);
            foreach (var propriedade in propriedades)
            {
                Assert.True(
                    schema.GetProperty("properties").TryGetProperty(propriedade, out var membro),
                    $"{nome} sem a propriedade {propriedade}");
                Assert.Equal("string", membro.GetProperty("type").GetString());
            }

            // O gerador marca os parâmetros não anuláveis do record como required;
            // a comparação exata pega tanto propriedade faltando quanto sobrando.
            var obrigatorias = schema.GetProperty("required")
                .EnumerateArray()
                .Select(valor => valor.GetString()!)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                propriedades.Order(StringComparer.Ordinal).ToArray(),
                obrigatorias);

            // O documento sozinho não prova que o handler devolve o record: um
            // anônimo com os mesmos nomes passaria. O corpo real fecha o laço.
            using var respostaHttp = await cliente.GetAsync(rota);
            respostaHttp.EnsureSuccessStatusCode();
            using var corpo = JsonDocument.Parse(await respostaHttp.Content.ReadAsStringAsync());
            var servidas = corpo.RootElement.EnumerateObject()
                .Select(propriedade => propriedade.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(obrigatorias, servidas);
        }
    }

    // O PDF público existe para baixar o arquivo: o 200 precisa declarar o media
    // type e um schema binário. Produces(200, null, ...) compila mas o gerador
    // descarta o content quando o tipo é nulo, então o media type sumiria do
    // contrato sem ninguém notar. O schema pode ser inline ou $ref para um schema
    // de string; o formato precisa ser `binary` (bytes crus), porque `byte`
    // mandaria o cliente base64-decodificar e corromper o arquivo.
    [Fact]
    public async Task PdfPublicoDeclaraMediaTypeBinario()
    {
        var documento = await LoadAsync();
        var operacao = Operacoes(documento)
            .Single(o => o.Metodo == "GET" && o.Rota == "/api/pescarias/{shareToken}/pdf").Operacao;

        Assert.True(
            operacao.GetProperty("responses").TryGetProperty("200", out var resposta),
            "GET /api/pescarias/{shareToken}/pdf sem resposta 200");

        Assert.True(
            resposta.TryGetProperty("content", out var content),
            "200 do PDF sem content");
        Assert.Equal(JsonValueKind.Object, content.ValueKind);
        Assert.True(
            content.TryGetProperty("application/pdf", out var pdf),
            "200 do PDF sem content application/pdf");

        Assert.True(
            pdf.TryGetProperty("schema", out var schema),
            "200 do PDF sem schema em application/pdf");

        if (schema.TryGetProperty("$ref", out var referencia))
        {
            var nome = referencia.GetString()!;
            Assert.StartsWith("#/components/schemas/", nome);
            schema = documento.RootElement
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty(nome["#/components/schemas/".Length..]);
        }

        Assert.Equal("string", schema.GetProperty("type").GetString());

        var formato = schema.TryGetProperty("format", out var valor) ? valor.GetString() : null;
        Assert.Equal("binary", formato);
    }

    // Todo 2xx com corpo tem que publicar o schema desse corpo — JSON, HTML ou o
    // PDF binário —, senão o cliente gera o tipo errado. O mesmo passo pega
    // Produces(200, null, "application/json"), que some com o content inteiro.
    [Fact]
    public async Task TodoSucessoComCorpoDeclaraSchema()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);
        var schemas = documento.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.Equal(54, operacoes.Count);

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var chave = (metodo, rota);
            foreach (var resposta in operacao.GetProperty("responses").EnumerateObject())
            {
                if (!int.TryParse(
                        resposta.Name,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var status)
                    || status is < 200 or > 299)
                {
                    continue;
                }

                var temConteudo = resposta.Value.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.Object
                    && content.EnumerateObject().Any();

                if (!RespostasEsperadas.TemCorpo(chave, status))
                {
                    if (temConteudo)
                    {
                        violacoes.Add($"{metodo} {rota} {status}: sucesso sem corpo declarou content");
                    }

                    continue;
                }

                if (!temConteudo)
                {
                    violacoes.Add($"{metodo} {rota} {status}: sucesso com corpo sem content");
                    continue;
                }

                foreach (var midia in content.EnumerateObject())
                {
                    if (!midia.Value.TryGetProperty("schema", out var schema)
                        || schema.ValueKind != JsonValueKind.Object)
                    {
                        violacoes.Add($"{metodo} {rota} {status} {midia.Name}: sucesso sem schema");
                        continue;
                    }

                    if (schema.TryGetProperty("$ref", out var referencia))
                    {
                        var nome = referencia.GetString() ?? string.Empty;
                        var prefixo = "#/components/schemas/";
                        if (!nome.StartsWith(prefixo, StringComparison.Ordinal)
                            || !schemas.TryGetProperty(nome[prefixo.Length..], out _))
                        {
                            violacoes.Add(
                                $"{metodo} {rota} {status} {midia.Name}: $ref não resolve em components.schemas ({nome})");
                        }

                        continue;
                    }

                    // PDF e HTML publicam schema inline; o type é o mínimo que o
                    // gerador de cliente precisa para não tratar o corpo como opaco.
                    if (!schema.TryGetProperty("type", out _))
                    {
                        violacoes.Add($"{metodo} {rota} {status} {midia.Name}: schema sem $ref e sem type");
                    }
                }
            }
        }

        Assert.Empty(violacoes);
    }

    // As quatro criações devolvem o recurso criado; trocar o Produces<T> por
    // outro DTO compila e passa no resto da suíte, mas quebraria o cliente que
    // lê o 201. A expectativa é literal, não derivada do endpoint.
    [Theory]
    [InlineData("/api/pescarias", nameof(PescariaSalvaResponse))]
    [InlineData("/api/locais", nameof(LocalDto))]
    [InlineData("/api/pontos", nameof(PontoDto))]
    [InlineData("/api/trilhas", nameof(TrilhaDto))]
    public async Task CriacaoDeclaraODtoDevolvido(string rota, string nomeDoSchema)
    {
        var documento = await LoadAsync();
        var operacao = Operacoes(documento)
            .Single(o => o.Metodo == "POST" && o.Rota == rota).Operacao;

        var criado = operacao.GetProperty("responses").GetProperty("201");
        var json = criado.GetProperty("content").GetProperty(RespostasPadrao.TipoJson);
        var referencia = json.GetProperty("schema").GetProperty("$ref").GetString()!;

        Assert.StartsWith("#/components/schemas/", referencia, StringComparison.Ordinal);
        Assert.EndsWith($"/{nomeDoSchema}", referencia, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogoDeCodigosDeErroEhCoerente()
    {
        Assert.Equal(17, CodigosDeErro.Todos.Count);
        foreach (var codigo in CodigosDeErro.Todos)
        {
            Assert.Matches("^[a-z]+(_[a-z]+)+$", codigo);
        }
    }

    /// <summary>
    /// Carrega /openapi/v1.json uma única vez por execução de teste; os
    /// invariantes leem o mesmo documento.
    /// </summary>
    private Task<JsonDocument> LoadAsync() =>
        _documento ??= CarregarContratoAsync();

    private async Task<JsonDocument> CarregarContratoAsync()
    {
        using var cliente = _fixture.Factory.CreateClient();
        using var resposta = await cliente.GetAsync("/openapi/v1.json");
        resposta.EnsureSuccessStatusCode();
        await using var fluxo = await resposta.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(fluxo);
    }

    private static List<(string Metodo, string Rota, JsonElement Operacao)> Operacoes(JsonDocument documento) =>
        documento.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(caminho => caminho.Value
                .EnumerateObject()
                .Select(metodo => (metodo.Name.ToUpperInvariant(), caminho.Name, metodo.Value)))
            .ToList();

    // Cada operação pertence a exatamente uma tag; sem ela o domínio não é
    // identificável e a theory não tem como isolar a sua área.
    private static string? Tag(JsonElement operacao) =>
        operacao.TryGetProperty("tags", out var tags) && tags.GetArrayLength() == 1
            ? tags[0].GetString()
            : null;

    private static string[] Esquemas(JsonElement security) =>
        security.EnumerateArray()
            .SelectMany(requisito => requisito.EnumerateObject().Select(esquema => esquema.Name))
            .ToArray();

    /// <summary>
    /// Resolve o endpoint real a partir de (método, rota) do contrato. Rota com
    /// restrição ("{id:guid}") é comparada sem a restrição, porque o OpenAPI
    /// publica só o nome do parâmetro.
    /// </summary>
    private RouteEndpoint Localizar(string metodo, string rota)
    {
        var endpoints = _fixture.Factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints;

        var encontrados = endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    .Contains(metodo, StringComparer.OrdinalIgnoreCase) == true
                && NormalizarRota(endpoint.RoutePattern.RawText) == NormalizarRota(rota))
            .ToList();

        return Assert.Single(encontrados);
    }

    private static string NormalizarRota(string? rota)
    {
        if (string.IsNullOrEmpty(rota))
        {
            return "/";
        }

        var semRestricao = Regex.Replace(rota, @"\{([^}:]+):[^}]+\}", "{$1}");
        return "/" + semRestricao.Trim('/');
    }
}
