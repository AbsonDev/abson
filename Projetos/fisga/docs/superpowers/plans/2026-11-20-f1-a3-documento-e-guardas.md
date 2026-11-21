# F1/A3 — identidade do documento, comportamentos e guardas: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** fechar a fase F1 do contrato: o `openapi.json` ganha identidade pública (`info`/`servers`), os comportamentos que surpreendem quem integra ficam escritos nele, o job `api` valida o documento com o lint de OpenAPI e o job `docs` falha quando contrato, `README.md` e inventário divergem.

**Architecture:** a identidade do documento (título, descrição, contato, licença, `servers` e descrição das 13 tags) entra no `ContratoDocumentTransformer`, que já é o dono do documento. Os comportamentos surpreendentes entram como descrição nas operações que os produzem e como regra transversal no `info.description`; o único exemplo que não é ambíguo (`200` do `/api/auth/me` sem sessão) entra como `example` da resposta via transformer. A guarda de rotas é um script Node sem dependências, com funções puras testadas, rodando no job `docs`.

**Tech Stack:** ASP.NET Core 10 Minimal APIs, `Microsoft.AspNetCore.OpenApi` 10.0.15 (Microsoft.OpenApi 2.13), xUnit, `WebApplicationFactory`, Node 24 (`node:test`), GitHub Actions, `@redocly/cli` 2.82.0.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-20-f1-contrato-verdadeiro-design.md`

**Issues:** #68 (F1.7), #70 (F1.9), #71 (F1.10) — as três fecham nesta PR.

**Antecessor:** `docs/superpowers/plans/2026-11-20-f1-a2-respostas-reais.md` (A2, mergeado em `bf5858f`).

## Global Constraints

- **Nenhuma mudança de comportamento da API.** Só metadata do contrato, script de verificação e documentação. O único acréscimo já autorizado na F1 além deste pacote (`Retry-After` no `429`) entrou no A2.
- `openapi.json` é artefato: gerado por `scripts/gerar-openapi.sh`, nunca editado à mão.
- Tudo em português do Brasil: descrições, resumos, mensagens do script e comentários novos.
- Contagem fixa: **54 operações**, 13 tags, 5 políticas de rate limit.
- Build/teste na raiz de `Projetos/fisga`, com `export PATH="$HOME/.dotnet:$PATH"`.
- Não tocar em `docs/ux-audit.md` (snapshot datado) nem em arquivos fora de `Projetos/fisga/**` e `.github/workflows/fisga-pipeline.yml`.
- O claim de #68 cobre o pacote inteiro; não criar claim para #70/#71.

## Decisões que o plano fixa

1. **Identidade no document transformer, não em `AddOpenApi`.** `ContratoDocumentTransformer` já é o dono do documento (schemes de segurança); `info`, `servers` e descrições de tag entram lá, num lugar só.
2. **`servers` fixo, na ordem produção → local**, porque o artefato commitado é gerado em Development e comparado byte a byte no CI. Escolher por ambiente faria o JSON mudar entre máquinas e o portal publicar ambiente morto.
3. **Título público `API do Fisga`.** O gerador publica `Fisga.Api | v1` (nome de assembly); o portal usa `info.title` no cabeçalho. `version` continua a gerada (`1.0.0`).
4. **Contato sem e-mail.** Não existe endereço público de suporte; `contact` leva `name: Fisga` e `url` do site. Licença proprietária: `license.name: Proprietária`, sem URL e sem identificador SPDX.
5. **Descrição das 13 tags entra neste pacote.** O lint `minimal` acusa `tag-description` em todas as 13; sem descrição a navegação do portal nasce vazia. É metadata do documento, mesma natureza do `info`.
6. **Regras transversais vivem no `info.description`.** `403 consentimento_pendente` e `403 origem_invalida` valem para quase toda rota `/api`, não para uma operação; repetir em 28 operações criaria 28 cópias. O `info.description` traz as duas regras e os dois corpos JSON, que é o lugar que a página de abertura do portal mostra.
7. **Exemplo machine-readable só onde não há ambiguidade.** O `200` de `/api/auth/me` sem sessão tem um corpo só; vira `example` da resposta. O `403` carrega três `codigo` possíveis, então o exemplo fica em prosa (fenced JSON) na descrição — um `example` único mentiria.
8. **Guarda por script Node puro, sem dependência.** O job `docs` já roda Node via `npx`; um `.mjs` com funções puras exportadas é testável com `node --test` e não adiciona pacote ao repositório.
9. **Lint no job `api`**, que é quem vê mudança de contrato, com versão fixa `@redocly/cli@2.82.0` e `--extends=minimal` (estrutura e referências; não impõe estilo).

## Review Focus

1. **`servers` mudando com o ambiente.** Se alguém trocar a lista fixa por leitura de configuração, o artefato commitado passa a depender de onde foi gerado. Teste compara as duas entradas e a ordem.
2. **`info.description` prometendo o que a API não faz.** As duas regras transversais já são reais hoje (middlewares de consentimento e CSRF); o texto precisa casar com as exceções (`/api/auth/me`, `/api/auth/sair`, `/api/conta/consentimento`).
3. **Exemplo do `/me` divergindo do corpo servido.** O exemplo é escrito à mão; se o `MeResponse` ganhar um campo, o exemplo fica velho. Teste lê o `example` e compara o conjunto de chaves com o schema do `MeResponse`.
4. **Descrição de redirect errada.** O texto de `confirmar`/`google/*` precisa dizer os três destinos reais (boas-vindas no primeiro acesso, `estado=ok` depois, `estado=<codigo>` no erro) — foi exatamente o drift que o `docs-ai` apontou no inventário.
5. **Guarda que passa sem ler tudo.** Um extrator que devolve conjunto vazio também "não diverge". Teste com fixture garante que as 54 linhas são lidas e que uma rota ausente em cada documento produz erro com o nome da rota.

---

### Task 1: Identidade do documento — `info`, `servers`, tags e lint (#68)

**Files:**
- Modify: `src/Fisga.Api/OpenApi/Tags.cs`
- Modify: `src/Fisga.Api/OpenApi/ContratoDocumentTransformer.cs`
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs`
- Modify: `.github/workflows/fisga-pipeline.yml` (job `api`)
- Regenerate: `docs/api/openapi.json`

**Interfaces:**
- Produces:
  - `Tags.Descricoes`: `IReadOnlyDictionary<string, string>` com uma entrada por tag de `Tags.Todas`.
  - `ContratoDocumentTransformer` passa a preencher `document.Info.{Title,Description,Contact,License}`, `document.Servers` e `tag.Description` de cada tag.
  - Testes `DocumentoTemIdentidadePublica` e `TagsTemDescricao` em `ContratoTests`.
- Consumes: `Tags.Todas` (já existe).

- [ ] **Step 1: Escrever os testes que falham**

Em `ContratoTests.cs`, depois de `ResumosSaoDeNegocio`:

```csharp
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
    Assert.Contains("POST /api/conta/consentimento", descricao);

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
```

- [ ] **Step 2: Rodar e ver falhar**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~DocumentoTemIdentidadePublica|FullyQualifiedName~TagsTemDescricao"
```

Esperado: falha em `DocumentoTemIdentidadePublica` (título é `Fisga.Api | v1`, `description`/`contact`/`license`/`servers` ausentes) e em `TagsTemDescricao` (sem `description`).

- [ ] **Step 3: Implementar as descrições das tags em `Tags.cs`**

Adicionar ao fim da classe (as 13 entradas, uma por constante — o dicionário é a fonte da descrição e o teste exige exatamente as chaves de `Todas`):

```csharp
    public static IReadOnlyDictionary<string, string> Descricoes { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Sistema] = "Saúde, prontidão e versões legais da API.",
            [Catalogo] = "Espécies, iscas e opções de referência do app.",
            [Pescarias] = "Registro da pescaria, visão pública, edição e PDF.",
            [Locais] = "Locais salvos, busca e geocodificação reversa.",
            [Pontos] = "Pontos privados do pescador.",
            [Trilhas] = "Trilhas privadas do pescador.",
            [Diario] = "Diário, estatísticas e insights do pescador.",
            [Midia] = "Upload de fotos com EXIF tratado no navegador.",
            [Especies] = "Sugestão de espécie a partir de uma foto.",
            [Condicoes] = "Clima, maré aproximada e comparação com o histórico.",
            [Autenticacao] = "Link mágico, senha, Google, sessão e perfil.",
            [Conta] = "Consentimento, senha, sessões, exportação e exclusão.",
            [Newsletter] = "Envio e prévia do digest mensal (token administrativo)."
        };
```

- [ ] **Step 4: Implementar a identidade no `ContratoDocumentTransformer`**

No início de `TransformAsync`, antes dos schemes:

```csharp
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "API do Fisga";
        document.Info.Description = DescricaoDoDocumento;
        document.Info.Contact = new OpenApiContact
        {
            Name = "Fisga",
            Url = new Uri("https://fisga.abson.tech")
        };
        document.Info.License = new OpenApiLicense { Name = "Proprietária" };

        // Fixo de propósito: o artefato é gerado em Development e comparado byte
        // a byte no CI. Escolher por ambiente faria o JSON mudar entre máquinas
        // e o portal publicar um endereço que não existe.
        document.Servers =
        [
            new OpenApiServer
            {
                Url = "https://api.fisga.abson.tech",
                Description = "Produção"
            },
            new OpenApiServer
            {
                Url = "http://localhost:5184",
                Description = "Local"
            }
        ];

        // document.Tags é ISet: a ordem vem do gerador e não é contrato.
        foreach (var tag in document.Tags)
        {
            tag.Description = Tags.Descricoes.TryGetValue(tag.Name, out var descricao)
                ? descricao
                : throw new InvalidOperationException(
                    $"Tag sem descrição em Tags.Descricoes: {tag.Name}");
        }
```

E a constante, no fim da classe:

```csharp
    private const string DescricaoDoDocumento = """
        API do Fisga: o diário de pesca que registra capturas com identidade própria.

        As operações são de leitura pública, exigem sessão ou exigem token
        administrativo; `security` em cada uma diz qual é o caso. A sessão é o
        cookie `fisga_auth`, com validade deslizante de 30 dias.

        **Erros.** Respostas de erro com corpo usam `{codigo, detalhe}`; exceção
        não tratada vira `500 erro_inesperado`. O `429` não tem corpo e traz o
        header `Retry-After` em segundos.

        **Consentimento.** Usuário autenticado com Termos ou Privacidade
        pendentes recebe `403` em qualquer rota `/api` que exija sessão, exceto
        `GET /api/auth/me`, `POST /api/auth/sair` e
        `POST /api/conta/consentimento`; rotas públicas seguem livres:

        ```json
        {"codigo":"consentimento_pendente","detalhe":"Aceite os termos e a privacidade vigentes para continuar."}
        ```

        **Origem.** Mutação (`POST`, `PUT`, `PATCH`, `DELETE`) com `Origin` de
        outro site recebe `403 origem_invalida`:

        ```json
        {"codigo":"origem_invalida","detalhe":"Origem da requisição não permitida."}
        ```

        As duas rotas administrativas respondem `404` sem corpo a token inválido,
        de propósito: não revelam que o token existe.
        """;
```

- [ ] **Step 5: Rodar os testes e ver passar**

```bash
dotnet build Fisga.slnx -c Release -warnaserror
dotnet test Fisga.slnx -c Release --no-build
```

Esperado: os dois testes novos verdes e os 404 anteriores também.

- [ ] **Step 6: Regerar o artefato e conferir a identidade**

```bash
./scripts/gerar-openapi.sh
git diff --stat docs/api/openapi.json
python3 -c "import json;d=json.load(open('docs/api/openapi.json'));print(d['info']);print(d['servers'])"
```

Esperado: `info` com título, descrição, contato e licença; `servers` com as duas entradas na ordem produção → local.

- [ ] **Step 7: Ligar o lint no job `api`**

Em `.github/workflows/fisga-pipeline.yml`, entre `Contrato documentado está atualizado` e `Publicar OpenAPI`:

```yaml
      - uses: actions/setup-node@v7.1.0
        with:
          node-version: ${{ env.NODE_VERSION }}

      - name: Lint do OpenAPI
        run: >
          npx --yes @redocly/cli@2.82.0 lint docs/api/openapi.json
          --extends=minimal
```

- [ ] **Step 8: Rodar o lint e conferir o resultado esperado**

```bash
npx --yes @redocly/cli@2.82.0 lint docs/api/openapi.json --extends=minimal --format=summary
```

Esperado: `valid` e exit 0. Warnings restantes depois desta task: 6 de `operation-2xx-response` (operações que só respondem `302`) e 2 de caminhos ambíguos entre `/api/pescarias/{shareToken}` e `/api/pescarias/{id}` — advisory, o job só falha com erro.

- [ ] **Step 9: Commit**

```bash
git add src/Fisga.Api/OpenApi/Tags.cs src/Fisga.Api/OpenApi/ContratoDocumentTransformer.cs tests/Fisga.Api.Tests/ContratoTests.cs docs/api/openapi.json ../../.github/workflows/fisga-pipeline.yml
git commit -m "docs(fisga): identidade do documento no contrato e lint no job api"
```

---

### Task 2: Comportamentos que surpreendem e exemplo do `/me` (#70)

**Files:**
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/NewsletterEndpoints.cs`
- Create: `src/Fisga.Api/OpenApi/ExemplosDeResposta.cs`
- Modify: `src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs`
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs`
- Modify: `tests/Fisga.Api.Tests/NormalizacaoDeRespostasTests.cs`
- Regenerate: `docs/api/openapi.json`

**Interfaces:**
- Produces:
  - `Fisga.Api.OpenApi.ExemplosDeResposta.Para(string metodo, string rota, int status)` → `(string Nome, string Resumo, string Json)?`, chave única hoje: `("GET", "/api/auth/me", 200)`.
  - `ContratoOperationTransformer.NormalizarRespostas(OpenApiOperation operation, string metodo, string rota)` (assinatura nova; a antiga de dois parâmetros deixa de existir).
- Consumes: `NormalizarRespostas` é chamado com `metodo`/`rota` já normalizados pelo `TransformAsync`.

- [ ] **Step 1: Escrever o teste que falha**

Em `ContratoTests.cs`:

```csharp
[Fact]
public async Task ComportamentosQueSurpreendemEstaoDocumentados()
{
    var documento = await LoadAsync();
    var operacoes = Operacoes(documento);

    string Descricao(string metodo, string rota) =>
        Operacao(documento, metodo, rota).GetProperty("description").GetString() ?? string.Empty;

    // 1. 404 proposital das rotas administrativas.
    foreach (var rota in new[] { "/api/newsletter/enviar", "/api/newsletter/preview" })
    {
        var metodo = rota.EndsWith("enviar", StringComparison.Ordinal) ? "POST" : "GET";
        var descricao = Descricao(metodo, rota);
        Assert.Contains("404 sem corpo", descricao, StringComparison.Ordinal);
        Assert.Contains("proposital", descricao, StringComparison.Ordinal);
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
    var me = Operacao(documento, "GET", "/api/auth/me");
    var exemplo = me.GetProperty("responses").GetProperty("200")
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

// Localiza a operação com diagnóstico legível: rota renomeada não pode falhar
// como "Sequence contains no matching element".
private static JsonElement Operacao(JsonDocument documento, string metodo, string rota)
{
    var encontrada = Operacoes(documento)
        .Where(operacao => operacao.Metodo == metodo && operacao.Rota == rota)
        .Select(operacao => operacao.Operacao)
        .ToList();
    Assert.True(
        encontrada.Count == 1,
        $"Operação ausente ou duplicada no contrato: {metodo} {rota}");
    return encontrada[0];
}
```

- [ ] **Step 2: Rodar e ver falhar**

```bash
dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ComportamentosQueSurpreendemEstaoDocumentados"
```

Esperado: falha em `proposital` (texto ainda não existe).

- [ ] **Step 3: Reescrever as descrições de auth**

Em `AuthEndpoints.cs`, substituir as quatro chamadas `WithDescription` por:

- `GET /api/auth/confirmar`:
  `"Funciona sem sessão e tem efeito colateral: consome o token, cria a sessão e responde 302, sem corpo. Primeiro acesso redireciona para `/boas-vindas?novo=1`; os seguintes, para `/entrar/confirmar?estado=ok`; erro de link, para `/entrar/confirmar?estado=<codigo>`. Não use como link idempotente."`
- `GET /api/auth/google/iniciar`:
  `"Funciona sem sessão. Redireciona 302 ao Google e responde 503 quando as credenciais não estão configuradas."`
- `GET /api/auth/google/concluir`:
  `"Funciona sem sessão e tem efeito colateral: é o retorno do Google, cria a sessão e responde 302, sem corpo. Primeiro acesso redireciona para `/boas-vindas?novo=1`; os seguintes, para `/entrar/confirmar?estado=ok`; falha, para `/entrar/confirmar?estado=<codigo>`."`
- `GET /api/auth/google/conectar/concluir`:
  ``"Exige sessão no cookie; sem sessão o redirect também é 302, para `/conta?estado=google_conexao_nao_autenticado`. Cria apenas o vínculo, sem trocar de usuário, e responde 302 sem corpo: sucesso vai para `/conta?estado=google_conectado`; falha, para `/conta?estado=<codigo>`."``

Manter `GET /api/auth/me` como está — a descrição já diz `autenticado falso`; o exemplo entra no passo 4.

- [ ] **Step 4: Criar `ExemplosDeResposta.cs`**

```csharp
namespace Fisga.Api.OpenApi;

/// <summary>
/// Exemplos de corpo publicados no contrato. Só entra aqui resposta cujo corpo
/// é único: um exemplo para um status que carrega códigos diferentes mentiria.
/// </summary>
public static class ExemplosDeResposta
{
    public static (string Nome, string Resumo, string Json)? Para(
        string metodo,
        string rota,
        int status) =>
        (metodo, rota, status) switch
        {
            ("GET", "/api/auth/me", 200) => (
                "semSessao",
                "Sem sessão: 200 com autenticado false, não erro.",
                """
                {"autenticado":false,"usuario":null,"consentimentoPendente":false,"googleConectado":false,"temSenha":false}
                """),
            _ => null
        };
}
```

- [ ] **Step 5: Aplicar o exemplo no transformer**

Trocar a assinatura e a chamada:

```csharp
        NormalizarRespostas(operation, metodo, rota);
```

```csharp
    internal static void NormalizarRespostas(OpenApiOperation operation, string metodo, string rota)
```

E, no fim de cada iteração do `foreach` (depois do bloco de descrição):

```csharp
            if (concreta.Content is not null
                && ExemplosDeResposta.Para(metodo, rota, status) is { } exemplo
                && concreta.Content.TryGetValue(RespostasPadrao.TipoJson, out var tipoJson))
            {
                tipoJson.Examples ??= new Dictionary<string, IOpenApiExample>();
                // Chave repetida é erro de catálogo, não algo a sobrescrever em
                // silêncio: o contrato publicaria um exemplo que ninguém revisou.
                if (!tipoJson.Examples.TryAdd(exemplo.Nome, new OpenApiExample
                    {
                        Summary = exemplo.Resumo,
                        Value = JsonNode.Parse(exemplo.Json)
                    }))
                {
                    throw new InvalidOperationException(
                        $"Exemplo duplicado no contrato: {exemplo.Nome} em {metodo} {rota} {status}");
                }
            }
```

(`JsonNode` já está importado em `ContratoOperationTransformer.cs`.)

- [ ] **Step 6: Atualizar os testes do transformer**

Em `NormalizacaoDeRespostasTests.cs`, as oito chamadas `NormalizarRespostas(Operacao(...))` passam a receber a rota neutra do arquivo: `ContratoOperationTransformer.NormalizarRespostas(Operacao("400", resposta), "GET", "/api/teste")`. Acrescentar o fato, usando o helper `Operacao` que já existe:

```csharp
[Fact]
public void ExemploDoMeVaiParaOMediaTypeJson()
{
    var resposta = new OpenApiResponse
    {
        Description = "OK",
        Content = new Dictionary<string, OpenApiMediaType>
        {
            [RespostasPadrao.TipoJson] = new() { Schema = new OpenApiSchema() }
        }
    };

    ContratoOperationTransformer.NormalizarRespostas(
        Operacao("200", resposta), "GET", "/api/auth/me");

    var exemplo = Assert.Contains("semSessao", resposta.Content![RespostasPadrao.TipoJson].Examples!);
    Assert.False(exemplo.Value!["autenticado"]!.GetValue<bool>());
}
```

- [ ] **Step 7: Rodar os testes e ver passar**

```bash
dotnet build Fisga.slnx -c Release -warnaserror
dotnet test Fisga.slnx -c Release --no-build
```

- [ ] **Step 8: Regerar e conferir o exemplo no artefato**

```bash
./scripts/gerar-openapi.sh
python3 -c "
import json
d=json.load(open('docs/api/openapi.json'))
print(json.dumps(d['paths']['/api/auth/me']['get']['responses']['200']['content']['application/json']['examples'],ensure_ascii=False,indent=1))"
```

- [ ] **Step 9: Commit**

```bash
git add src/Fisga.Api/Endpoints/AuthEndpoints.cs src/Fisga.Api/Endpoints/NewsletterEndpoints.cs src/Fisga.Api/OpenApi/ExemplosDeResposta.cs src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs tests/Fisga.Api.Tests/ContratoTests.cs tests/Fisga.Api.Tests/NormalizacaoDeRespostasTests.cs docs/api/openapi.json
git commit -m "docs(fisga): documenta os comportamentos que surpreendem no contrato"
```

---

### Task 3: Guarda contra divergência entre contrato, README e inventário (#71)

**Files:**
- Create: `scripts/verificar-rotas.mjs`
- Create: `scripts/verificar-rotas.test.mjs`
- Modify: `.github/workflows/fisga-pipeline.yml` (job `docs`)
- Modify: `docs/guias/como-adicionar-endpoint.md`

**Interfaces:**
- Produces:
  - `extrairDoContrato(documento: object): Set<string>` — itens `"METODO /rota"`.
  - `extrairDoReadme(markdown: string): Set<string>`
  - `extrairDoInventario(markdown: string): Set<string>`
  - `divergencias(contrato: Set<string>, readme: Set<string>, inventario: Set<string>): { faltamNoReadme: string[], faltamNoInventario: string[], sobramNoReadme: string[], sobramNoInventario: string[] }` — listas ordenadas.
  - CLI sem argumentos: lê `docs/api/openapi.json`, `README.md` e `docs/api/inventario.md` a partir da raiz do projeto resolvida por `import.meta.url`; exit 1 listando as divergências, exit 0 com `Rotas em sincronia: N operações no contrato, no README e no inventário.`
- Consumes: nada.

- [ ] **Step 1: Escrever o teste que falha**

`scripts/verificar-rotas.test.mjs`:

```js
import test from 'node:test';
import assert from 'node:assert/strict';
import {
  extrairDoContrato,
  extrairDoReadme,
  extrairDoInventario,
  divergencias
} from './verificar-rotas.mjs';

test('contrato publica metodo e rota de cada operacao', () => {
  const documento = {
    paths: {
      '/health': { get: {} },
      '/api/x/{id}': { get: {}, delete: {} },
      '/api/x': { post: {} }
    }
  };
  assert.deepEqual(
    [...extrairDoContrato(documento)].sort(),
    ['DELETE /api/x/{id}', 'GET /api/x/{id}', 'GET /health', 'POST /api/x']);
});

test('README le a tabela do resumo, apara query e separa celula combinada', () => {
  const markdown = [
    '# Projeto',
    '## API',
    '',
    '| Método | Rota | Acesso | Descrição |',
    '| --- | --- | --- | --- |',
    '| `GET` | `/health`, `/ready` | Público | Saúde |',
    '| `GET` | `/api/auth/confirmar?token=` | Público | Confirma |',
    '| `POST` | `/api/pescarias` | Sessão | Cria |',
    '',
    '## Privacidade',
    '| `GET` | `/api/fora-da-secao` | X | Y |'
  ].join('\n');
  assert.deepEqual(
    [...extrairDoReadme(markdown)].sort(),
    ['GET /api/auth/confirmar', 'GET /health', 'GET /ready', 'POST /api/pescarias']);
});

test('inventario le as linhas de operacao da secao Dominios', () => {
  const markdown = [
    '## Regras transversais',
    '| `GET /api/fora` | `public` |',
    '## Domínios',
    '### Sistema',
    '| `GET /health` | P | — | no-store | `200` | — |',
    '| `POST /api/newsletter/enviar` | A | — | no-store | `200` | `404` |',
    '## Lacunas de contrato'
  ].join('\n');
  assert.deepEqual(
    [...extrairDoInventario(markdown)].sort(),
    ['GET /health', 'POST /api/newsletter/enviar']);
});

test('divergencia lista o que falta em cada documento', () => {
  const contrato = new Set(['GET /health', 'GET /api/novo']);
  const readme = new Set(['GET /health']);
  const inventario = new Set(['GET /health', 'GET /api/antigo']);

  assert.deepEqual(divergencias(contrato, readme, inventario), {
    faltamNoReadme: ['GET /api/novo'],
    faltamNoInventario: ['GET /api/novo'],
    sobramNoReadme: ['GET /api/antigo'],
    sobramNoInventario: ['GET /api/antigo']
  });
});
```

- [ ] **Step 2: Rodar e ver falhar**

```bash
node --test scripts/verificar-rotas.test.mjs
```

Esperado: `ERR_MODULE_NOT_FOUND`.

- [ ] **Step 3: Implementar `scripts/verificar-rotas.mjs`**

Estrutura (funções puras exportadas + CLI no fim):

```js
#!/usr/bin/env node
// Compara as rotas do contrato com o resumo do README e com o inventário.
// O contrato é a fonte; README e inventário são texto mantido à mão e por isso
// podem divergir sem ninguém notar. Roda no job `docs`.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const METODOS = ['get', 'post', 'put', 'patch', 'delete'];
const LINHA_DE_OPERACAO = /^`(GET|POST|PUT|PATCH|DELETE) (\/\S+)`$/;

export function extrairDoContrato(documento) {
  const rotas = new Set();
  for (const [rota, operacoes] of Object.entries(documento.paths ?? {})) {
    for (const metodo of Object.keys(operacoes)) {
      if (METODOS.includes(metodo)) {
        rotas.add(`${metodo.toUpperCase()} ${rota}`);
      }
    }
  }
  return rotas;
}

export function extrairDoReadme(markdown) {
  const rotas = new Set();
  for (const linha of linhasDaSecao(markdown, '## API')) {
    const celulas = linha.split('|').map((celula) => celula.trim());
    if (celulas.length < 4) continue;
    const metodo = celulas[1].replaceAll('`', '');
    if (!METODOS.includes(metodo.toLowerCase())) continue;
    for (const bruta of celulas[2].split(',')) {
      const rota = bruta.trim().replaceAll('`', '').split('?')[0];
      if (rota.startsWith('/')) rotas.add(`${metodo.toUpperCase()} ${rota}`);
    }
  }
  return rotas;
}

export function extrairDoInventario(markdown) {
  const rotas = new Set();
  for (const linha of linhasDaSecao(markdown, '## Domínios')) {
    const celulas = linha.split('|').map((celula) => celula.trim());
    if (celulas.length < 3) continue;
    const encontrada = celulas[1].match(LINHA_DE_OPERACAO);
    if (encontrada) rotas.add(`${encontrada[1]} ${encontrada[2].split('?')[0]}`);
  }
  return rotas;
}

export function divergencias(contrato, readme, inventario) {
  const ordenar = (conjunto) => [...conjunto].sort();
  return {
    faltamNoReadme: ordenar([...contrato].filter((rota) => !readme.has(rota))),
    faltamNoInventario: ordenar([...contrato].filter((rota) => !inventario.has(rota))),
    sobramNoReadme: ordenar([...readme].filter((rota) => !contrato.has(rota))),
    sobramNoInventario: ordenar([...inventario].filter((rota) => !contrato.has(rota)))
  };
}
```

`linhasDaSecao(markdown, titulo)` devolve as linhas entre a linha exatamente igual a `titulo` e a próxima linha que começa com `## ` (exclusive); o CLI monta o relatório com os títulos `Faltam no README.md:` / `Faltam no inventário:` / `Sobram no README.md (não estão no contrato):` / `Sobram no inventário (não estão no contrato):`, uma linha `  - ${rota}` por item, e sai com 1 quando há qualquer lista não vazia. Raiz do projeto:

```js
const raiz = fileURLToPath(new URL('..', import.meta.url));
```

CLI executado só quando `process.argv[1]` é o próprio módulo (`pathToFileURL(process.argv[1]).href === import.meta.url`).

- [ ] **Step 4: Rodar o teste e o script**

```bash
node --test scripts/verificar-rotas.test.mjs
node scripts/verificar-rotas.mjs
```

Esperado: 4 testes verdes; script imprime `Rotas em sincronia: 54 operações no contrato, no README e no inventário.` e exit 0.

- [ ] **Step 5: Ligar no job `docs`**

Em `.github/workflows/fisga-pipeline.yml`, no job `docs`: o `Setup Node` entra imediatamente depois do `actions/checkout` (antes do `Markdown`, que também usa `npx`); a guarda e os testes dela entram entre `Markdown` e `Links`.

```yaml
      - name: Setup Node
        uses: actions/setup-node@v7.1.0
        with:
          node-version: ${{ env.NODE_VERSION }}
```

```yaml
      - name: Contrato, README e inventário em sincronia
        run: node Projetos/fisga/scripts/verificar-rotas.mjs

      - name: Testes da guarda de rotas
        run: node --test Projetos/fisga/scripts/verificar-rotas.test.mjs
```

- [ ] **Step 6: Documentar no guia de endpoint**

Em `docs/guias/como-adicionar-endpoint.md`:

- No §7, item 4 (registrar a operação nos testes), acrescente uma frase final apontando que a mesma operação precisa entrar no `docs/api/inventario.md` e na tabela `## API` do `README.md`, ambos guardados pelo script.
- No §8 (`Documentação`), troque o bullet do README por uma lista curta com a ordem: `docs/api/inventario.md` → tabela `## API` do `README.md` → `node scripts/verificar-rotas.mjs`, explicando que a guarda roda no job `docs` e falha listando a rota que falta em cada documento.

- [ ] **Step 7: Commit**

```bash
git add scripts/verificar-rotas.mjs scripts/verificar-rotas.test.mjs ../../.github/workflows/fisga-pipeline.yml docs/guias/como-adicionar-endpoint.md
git commit -m "test(fisga): guarda de rotas entre contrato, README e inventário"
```

---

### Task 4: Sincronização da documentação e preparação da PR

**Files:**
- Modify: `docs/api/inventario.md`
- Modify: `docs/api/README.md`
- Modify: `docs/api/backlog.md`
- Modify: `docs/adr/0017-portal-publico-da-api.md`
- Modify: `docs/ci-cd.md`
- Modify: `docs/review.md`
- Modify: `docs/roadmap.md`
- Modify: `README.md`
- Modify: `AGENTS.md`
- Modify: `docs/INDEX.md`
- Commit: `docs/superpowers/plans/2026-11-20-f1-a3-documento-e-guardas.md` (hoje untracked) e `docs/superpowers/specs/2026-11-20-f1-contrato-verdadeiro-design.md` (hoje modificada)

**Interfaces:**
- Consumes: identidade (Task 1), comportamentos (Task 2), guarda e lint (Tasks 1 e 3).
- Produces: documentação coerente com o artefato; nenhuma interface de código.

- [ ] **Step 1: Inventário (`docs/api/inventario.md`)**

- Cabeçalho `**Estado:**`: acrescentar o A3 (`info`/`servers` e comportamentos) e registrar a **F1 concluída** em 2026-11-20, mantendo o link para `../api/openapi.json` e a contagem de 54 operações em 48 rotas.
- `## Lacunas de contrato`: apagar a tabela e a nota "Resolvido no A2", substituindo por uma frase: "Nenhuma lacuna conhecida: `info`, `servers`, status reais, erros, uploads e `Retry-After` saem do contrato." Deixar claro que a seção fica como registro de que não há pendência.
- Linhas de redirect em `### Autenticação & Sessão` (o drift que o `docs-ai` apontou):
  - `GET /api/auth/confirmar`: trocar "Sucesso e erro redirecionam com `estado=<codigo>`" por "Primeiro acesso vai para `/boas-vindas?novo=1`; os seguintes, para `/entrar/confirmar?estado=ok`; erro de link, para `/entrar/confirmar?estado=<codigo>`".
  - `GET /api/auth/google/concluir`: mesma tríade; manter "primeiro acesso vai para `/boas-vindas`" só se a frase completa entrar.
  - `GET /api/auth/google/conectar/concluir`: acrescentar que sem sessão o redirect também é `302` para `/conta?estado=google_conexao_nao_autenticado`, e que falha vai para `/conta?estado=<codigo>`.
- `## Como manter`: acrescentar o item 4 — "Mexeu em rota? Rode `node scripts/verificar-rotas.mjs`; a guarda compara contrato, `README.md` e este inventário e roda no job `docs`."
- `## Verificação em execução local`: **não** reescrever a tabela datada; ela é registro de 2026-11-20.

- [ ] **Step 2: README do contrato (`docs/api/README.md`)**

- `## Limites conhecidos`: substituir o bullet "O contrato expressa respostas e erros; falta a identidade do documento" por dois bullets:
  - **Identidade fixa de propósito.** `info` (título, descrição, contato, licença) e `servers` (produção → `http://localhost:5184`) são escritos no transformer, não lidos do ambiente: o artefato commitado é regenerado no CI e comparado byte a byte.
  - **Lint advisory.** O job `api` valida o documento com `@redocly/cli` (`--extends=minimal`) e o job `docs` compara contrato, `## API` do `README.md` e inventário. Sobram 9 warnings advisory: 6 de operação que só responde `302`, 1 de `servers` apontando para `localhost`, 1 de caminho idêntico e 1 de caminho ambíguo entre `{shareToken}` e `{id}`.
- Se o texto ainda disser que o portal "ainda não existe", conferir contra a F2 e não prometer o que não está no ar.

- [ ] **Step 3: Backlog (`docs/api/backlog.md`)**

Marcar F1.7, F1.9 e F1.10 com `- **Estado:** concluída no A3 (2026-11-20) — esta PR fecha #68/#70/#71.`, no mesmo formato de F1.8. Se a seção da fase tiver um parágrafo de estado, atualizar para "F1 concluída".

- [ ] **Step 4: ADR 0017**

- Na consequência sobre o contrato, registrar que a F1 fechou: `info`/`servers` fixos, lint no job `api` e guarda de rotas no job `docs`.
- Corrigir o drift: o ADR diz que o contrato é "comparado no job `docs`"; a regeneração/comparação é do job **`api`**. A guarda de rotas é que vive no `docs`.

- [ ] **Step 5: `docs/ci-cd.md`**

- Árvore do topo: `api` passa a "restore locked + format + build + **408** testes + contrato + lint OpenAPI"; `docs` passa a "markdownlint + guarda de rotas + links".
- Seção `### api`: acrescentar `npx --yes @redocly/cli@2.82.0 lint docs/api/openapi.json --extends=minimal` (depois do `git diff --exit-code` do contrato) e atualizar a contagem de testes.
- Seção `### docs`: listar, na ordem real, `actions/setup-node` (Node 24), `npx markdownlint-cli2@0.24.1`, `node Projetos/fisga/scripts/verificar-rotas.mjs`, `node --test Projetos/fisga/scripts/verificar-rotas.test.mjs` e `lychee`. Explicar em uma frase que a guarda falha listando a rota que falta no `README.md` ou no inventário, e que o job só roda em PR.

- [ ] **Step 6: Contagens e estado**

Medir com `dotnet test Fisga.slnx -c Release --no-build` e somar as três dlls (esperado **408**). Atualizar:
- `README.md`: "Atualmente: **408 testes de backend** (46 de domínio + 58 de aplicação + 304 de API)" e o bullet do job `api` (acrescentar o lint de OpenAPI).
- `docs/review.md`: a linha do `gate` (total de testes) e a menção ao total no
  `require_tests_review`.
- `docs/roadmap.md`: o total de testes do `gate` e a frase sobre validação do contrato no job `api`/guarda no `docs`.
- **Não** tocar em `docs/ux-audit.md` (snapshot datado).

- [ ] **Step 7: `AGENTS.md` do Fisga**

A seção "Nunca" diz que "a tabela `## API` do `README.md` é resumo de produto e continua sendo atualizada à mão". A guarda agora exige **toda** operação nos dois documentos. Reescrever para: o contrato é a fonte; a tabela `## API` e o inventário listam **todas** as operações e são guardados por `scripts/verificar-rotas.mjs` no job `docs`.

- [ ] **Step 8: `docs/INDEX.md`**

Indexar este plano em `docs/superpowers/plans/`, no mesmo formato dos planos do A1/A2.

- [ ] **Step 9: Verificação final**

```bash
export PATH="$HOME/.dotnet:$PATH"
npx --yes markdownlint-cli2@0.24.1
dotnet build Fisga.slnx -c Release -warnaserror
dotnet test Fisga.slnx -c Release --no-build
node scripts/verificar-rotas.mjs
node --test scripts/verificar-rotas.test.mjs
npx --yes @redocly/cli@2.82.0 lint docs/api/openapi.json --extends=minimal
./scripts/gerar-openapi.sh && git diff --exit-code docs/api/openapi.json
git grep -nE "404\s+testes" -- . ':!docs/ux-audit.md' || true
```

Esperado: markdownlint 0, build 0 avisos, 408 testes verdes, guarda em sincronia, lint válido,
artefato idempotente, nenhuma menção ao total antigo de testes fora do snapshot.

- [ ] **Step 10: Commit**

```bash
git add docs README.md AGENTS.md
git commit -m "docs(fisga): fecha a fase F1 no inventário, backlog e runbooks"
```

O commit inclui o plano deste A3 e a correção da spec (porta 5184 e escopo do 403), que estavam fora dos commits das tasks.
