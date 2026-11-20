# F1/A1 — identidade e segurança do contrato: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dar a cada uma das 54 operações do Fisga tag de domínio, `operationId` congelado, resumo/descrição em português, segurança explícita e as extensões `x-rate-limit`/`x-cache`, sem mudar comportamento.

**Architecture:** a metadata entra onde o endpoint já é declarado (`.WithTags`, `.WithSummary`, `.WithDescription`); `operationId`, `security` e as extensões são derivados por um `IOpenApiOperationTransformer` a partir da rota, de `EndpointMetadata` e de um mapa congelado em código. Os schemes e o `info` ficam num `IOpenApiDocumentTransformer`. O JSON continua sendo artefato gerado por `scripts/gerar-openapi.sh`.

**Tech Stack:** ASP.NET Core 10 Minimal APIs, `Microsoft.AspNetCore.OpenApi`, xUnit, `WebApplicationFactory`.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-20-f1-contrato-verdadeiro-design.md`

## Global Constraints

- Nenhuma mudança de comportamento: só metadata. `Retry-After`, status e multipart são do A2.
- `openapi.json` é gerado por `scripts/gerar-openapi.sh`; nunca editar à mão.
- Tudo em português do Brasil: tags, `summary`, `description`, `operationId`.
- Tags permitidas, exatamente estas 13: `Sistema`, `Catálogo`, `Pescarias`, `Locais`, `Pontos`, `Trilhas`, `Diário`, `Mídia`, `Espécies`, `Condições`, `Autenticação`, `Conta`, `Newsletter`.
- Contagem de operações: **54** (27 GET, 18 POST, 4 PUT, 4 DELETE, 1 PATCH).
- `operationId` é a chave `"<MÉTODO> <rota sem barra inicial>"` no mapa; exatamente 54 entradas, sem duplicata.
- Quatro operações ficam **sem** `x-rate-limit`: `/health`, `/ready`, `POST /api/newsletter/enviar`, `GET /api/newsletter/preview`.
- `x-cache` existe nas 54; sem `CachePolicy` o valor é `private, no-store`.
- Build/teste na raiz de `Projetos/fisga`, com `export PATH="$HOME/.dotnet:$PATH"`.

## Review Focus

1. Rota com parâmetro (`{shareToken}`, `{id:guid}`) divergindo do mapa → `operationId` errado e URL do portal quebrada na F2. Teste: bijection spec ↔ mapa.
2. `AllowAnonymous` em rota que exige sessão (ou vice-versa) → contrato promete público e o leitor toma `401`. Teste: security por operação.
3. Nome/case da política de rate limit lido errado → número errado exibido ao integrador. Teste: valor igual a `RateLimiting:<política>` com configuração de teste.
4. `/openapi/v1.json` indisponível fora de Development → teste verde local e vermelho no CI. Mitigação: a factory de teste já usa `UseEnvironment("Development")`.
5. `summary` copiado do código (jargão, nome de handler) em vez de propósito de negócio → página inútil na F2. Teste: resumo não vazio, sem identificador de código (`{`, `(`, nome de classe).

---

### Task 1: Esqueleto do contrato e testes invariantes da A1

**Files:**
- Create: `Projetos/fisga/src/Fisga.Api/OpenApi/Tags.cs` (as 13 constantes)
- Create: `Projetos/fisga/src/Fisga.Api/OpenApi/MapaDeOperacoes.cs` (dicionário vazio + `Para`)
- Create: `Projetos/fisga/tests/Fisga.Api.Tests/ContratoTests.cs`
- Test: o próprio arquivo

**Interfaces:**
- Consumes: `ApiTestFixture` (collection `"api"`) e `Factory.CreateClient()`.
- Produces:
  - `Tags.Sistema` … `Tags.Newsletter` e `Tags.Todas` (`IReadOnlyList<string>`).
  - `MapaDeOperacoes.Todos` (`IReadOnlyDictionary<string,string>`, vazio nesta task) e `Para(string metodo, string rota)`.
  - `ContratoTests.LoadAsync()` → `JsonDocument` do `/openapi/v1.json`; helpers `Operacoes()` → `IEnumerable<(string Metodo, string Rota, JsonElement Op)>`.

O esqueleto entra junto para o projeto continuar compilando: os testes falham
por asserção, não por símbolo ausente.

- [ ] **Step 1: Criar `Tags` e `MapaDeOperacoes` vazios**

```csharp
public static class Tags
{
    public const string Sistema = "Sistema";
    // … Catálogo, Pescarias, Locais, Pontos, Trilhas, Diário, Mídia, Espécies,
    // Condições, Autenticação, Conta, Newsletter
    public static IReadOnlyList<string> Todas { get; } = [Sistema, /* … */];
}
```

- [ ] **Step 2: Escrever os testes que falham**

Arquivo com `[Collection("api")]`, um `JsonDocument` carregado uma vez (lazy estático `Task<JsonDocument>`) e quatro testes:

```csharp
[Fact] public void TodaOperacaoTemOperationIdDoMapa()
// 54 operações; cada (metodo, rota) resolve em MapaDeOperacoes.Para; ids únicos.

[Fact] public void ResumosSaoDeNegocio()
// summary não vazio e sem "Fisga.Api" / "{" / "(" / "Endpoints".
// Teste separado do de operationId: cada task tem seu próprio GREEN. Ele fica
// vermelho até a Task 3c, que é quem escreve os últimos resumos.

[Fact] public void TagsSaoDeDominio()
// tags de todas as operações ⊆ Tags.Todas (13); nenhuma "Fisga.Api";
// cada uma das 13 aparece em pelo menos uma operação.

[Fact] public void SegurancaExpressaPorOperacao()
// toda operação tem a chave "security" (mesmo que []); anônimas têm [];
// as demais citam "cookieAuth"; as duas de newsletter citam "adminToken"
// e nunca "cookieAuth"; schemes cookieAuth (cookie fisga_auth) e adminToken
// (header X-Admin-Token) existem em components.securitySchemes.

[Fact] public void RateLimitECacheRefletemMetadata()
// x-cache presente nas 54 e igual a CachePolicy.HeaderValue do endpoint ou
// "private, no-store"; x-rate-limit presente nas 50, ausente nas 4 isentas;
// requests igual a RateLimiting:<policy> ou ao default da tabela
// (leitura 180, escrita 40, auth 8, pdf 30, midia 30).
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `export PATH="$HOME/.dotnet:$PATH" && dotnet test Fisga.slnx -c Release --filter ContratoTests`
Expected: compila; os quatro testes falham por asserção (mapa vazio, sem tags, sem `security`).

- [ ] **Step 3: Commit**

```bash
git add src/Fisga.Api/OpenApi tests/Fisga.Api.Tests/ContratoTests.cs
git commit -m "test(fisga): invariantes do contrato por operação (F1/A1)"
```

---

### Task 2: Mapa de operações e `operationId`

**Files:**
- Modify: `Projetos/fisga/src/Fisga.Api/OpenApi/MapaDeOperacoes.cs` (preencher as 54 entradas)
- Create: `Projetos/fisga/src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs`
- Modify: `Projetos/fisga/src/Fisga.Api/Program.cs:45` (`AddOpenApi`)
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs` (teste 1)

**Interfaces:**
- Consumes: nada.
- Produces:
  - `public static class MapaDeOperacoes` com `public static IReadOnlyDictionary<string,string> Todos { get; }` e `public static string? Para(string metodo, string rota)`; chave `"GET api/pescarias/{shareToken}"`, rota sem barra inicial.
  - `public static class Tags` com `public const string Sistema = "Sistema";` … `Newsletter`, e `public static IReadOnlyList<string> Todas { get; }`.
  - `ContratoOperationTransformer : IOpenApiOperationTransformer` que, por operação, resolve `context.Description.HttpMethod` + `context.Description.RelativePath` no mapa e lança `InvalidOperationException($"Operação sem operationId: {metodo} {rota}")` quando falta; escreve `operation.OperationId`.

- [ ] **Step 1: Implementar o mapa com as 54 entradas**

Copiar do design a lista de nomes; a chave é método + rota exata do spec. Conferir uma a uma contra `docs/api/openapi.json` (ex.: `"GET api/pescarias/{shareToken}/permissao"` → `obterPermissaoPescaria`; `"PATCH api/auth/perfil"` → `atualizarPerfil`; `"DELETE api/conta"` → `excluirConta`).

- [ ] **Step 2: Implementar o transformer e registrar**

```csharp
builder.Services.AddOpenApi(opcoes =>
{
    opcoes.AddOperationTransformer<ContratoOperationTransformer>();
});
```

- [ ] **Step 3: Rodar o teste 1**

Run: `dotnet test Fisga.slnx -c Release --filter TodaOperacaoTemOperationIdDoMapa`
Expected: PASS. Os demais continuam vermelhos (tags, segurança, extensões, resumos).

- [ ] **Step 4: Commit**

```bash
git add src/Fisga.Api/OpenApi src/Fisga.Api/Program.cs
git commit -m "feat(fisga): operationId congelado por operação no contrato"
```

---

### Task 3a: Tags e descrições — Pescarias, Catálogo, Diário, Mídia, Locais, Pontos, Trilhas, Condições

**Files:**
- Modify: `src/Fisga.Api/Endpoints/PescariaEndpoints.cs` (Catálogo, Pescarias, Diário e Mídia — os dois últimos moram neste arquivo, não no `Program.cs`)
- Modify: `src/Fisga.Api/Endpoints/CondicoesEndpoints.cs` (Condições, Locais)
- Modify: `src/Fisga.Api/Endpoints/PontosEndpoints.cs` (Pontos)
- Modify: `src/Fisga.Api/Endpoints/TrilhasEndpoints.cs` (Trilhas)
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs` (tag por operação)

**Interfaces:**
- Consumes: `Tags.*` da Task 1.
- Produces: cada operação deste grupo com exatamente uma tag, `Summary` e `Description`.

- [ ] **Step 1: Aplicar tag por grupo quando houver grupo**

`app.MapGroup("/api/pescarias").WithTags(Tags.Pescarias)`; idem para os grupos de locais, pontos e trilhas. Endpoints soltos ganham `.WithTags(Tags.X)`.

- [ ] **Step 2: Escrever resumo e descrição das operações do grupo**

Regra: `summary` = uma frase de propósito no imperativo ("Cria uma pescaria", "Lista os locais do pescador"); `description` = uma ou duas frases de comportamento (o que muda, o que exige, o que retorna diferente). Proibido citar nome de handler, tipo C# ou `(HttpContext …)`, e **proibido parênteses no `summary`** — o teste rejeita `(` e `{`.

- [ ] **Step 3: Rodar e ver falhar o restante**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: o teste de tag falha listando as operações de auth/newsletter ainda sem tag — é a prova de que o teste cobre o que falta. O teste 1 continua verde.

- [ ] **Step 4: Commit**

```bash
git add src/Fisga.Api/Endpoints
git commit -m "docs(fisga): tags e descrições de pescarias, locais, pontos, trilhas e condições"
```

---

### Task 3b: Tags e descrições — Autenticação, Conta, Sistema

**Files:**
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Program.cs` (`/health`, `/ready`)
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs`

**Interfaces:**
- Consumes: `Tags.*`.
- Produces: 16 operações de auth com tag `Autenticação` (inclui `me`, `perfil` e `confirmar-email`), 5 de conta com `Conta`, 3 de sistema com `Sistema`.

- [ ] **Step 1: Aplicar tags e escrever resumo/descrição**

Mesma regra da 3a. `/me` descreve o `200` sem sessão; `/sair` descreve o `204` sem sessão (o contrato aprofunda no A3).

- [ ] **Step 2: Rodar**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: tag ainda falha só por newsletter/catálogo/diário/mídia/espécies.

- [ ] **Step 3: Commit**

```bash
git add src/Fisga.Api/Endpoints/AuthEndpoints.cs src/Fisga.Api/Program.cs
git commit -m "docs(fisga): tags e descrições de autenticação, conta e sistema"
```

---

### Task 3c: Tags e descrições — Newsletter e Espécies

**Files:**
- Modify: `src/Fisga.Api/Endpoints/NewsletterEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/VisionEndpoints.cs` (`/api/especies/sugerir`)
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs` (tags e resumos)

**Interfaces:**
- Consumes: `Tags.*`.
- Produces: as operações restantes com tag, `Summary` e `Description`; teste 2 verde.

- [ ] **Step 1: Aplicar tags e escrever resumo/descrição**

Nas duas administrativas, a descrição diz que exigem `X-Admin-Token` e respondem `404` quando o token é inválido (o A3 detalha o porquê).

- [ ] **Step 2: Rodar os testes**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: operationId e tags PASS; `ResumosSaoDeNegocio` fecha aqui, junto com as últimas operações.

- [ ] **Step 3: Commit**

```bash
git add src/Fisga.Api/Endpoints/NewsletterEndpoints.cs src/Fisga.Api/Endpoints/VisionEndpoints.cs
git commit -m "docs(fisga): tags e descrições de newsletter e espécies"
```

---

### Task 4: Segurança por operação

**Files:**
- Create: `src/Fisga.Api/OpenApi/RequerTokenAdmin.cs`
- Create: `src/Fisga.Api/OpenApi/ContratoDocumentTransformer.cs` (schemes + `info` básico fica para A3; aqui só schemes)
- Modify: `src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs` (`security`)
- Modify: `src/Fisga.Api/Endpoints/NewsletterEndpoints.cs` (`.WithMetadata(new RequerTokenAdmin())`)
- Modify: `src/Fisga.Api/Program.cs` (registrar o document transformer)
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs` (teste 3)

**Interfaces:**
- Consumes: `IOpenApiDocumentTransformer`/`IOpenApiOperationTransformer`.
- Produces: `components.securitySchemes.cookieAuth` (`apiKey`, `in: cookie`, `name: fisga_auth`) e `adminToken` (`apiKey`, `in: header`, `name: X-Admin-Token`); `security` presente em todas as operações.

- [ ] **Step 1: Implementar scheme e regra**

Regra do transformer: `RequerTokenAdmin` → `[{adminToken: []}]`; senão `IAllowAnonymous` → `[]`; senão `[{cookieAuth: []}]`. A ordem importa: o admin tem `AllowAnonymous` e não pode cair no ramo anônimo.

- [ ] **Step 2: Descrição dos schemes**

`cookieAuth`: "Sessão do Fisga no cookie `fisga_auth` (`HttpOnly`, `SameSite=Lax`; `Secure` conforme configuração; validade deslizante de 30 dias)." `adminToken`: "Token administrativo no header `X-Admin-Token`."

- [ ] **Step 3: Rodar o teste 3**

Expected: PASS; os testes 1 e 2 continuam verdes.

- [ ] **Step 4: Commit**

```bash
git add src/Fisga.Api/OpenApi src/Fisga.Api/Endpoints/NewsletterEndpoints.cs src/Fisga.Api/Program.cs
git commit -m "feat(fisga): segurança explícita por operação no contrato"
```

---

### Task 5: `x-rate-limit` e `x-cache`

**Files:**
- Modify: `src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs`
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs` (teste 4)

**Interfaces:**
- Consumes: `EnableRateLimitingAttribute.PolicyName`, `CachePolicy` (metadata), `IConfiguration`.
- Produces: extensões por operação.

- [ ] **Step 1: Implementar a tabela de defaults e as extensões**

`x-rate-limit` = `{ policy, requests, windowSeconds: 60 }` quando há `EnableRateLimitingAttribute`; ausente caso contrário. `requests` = `configuration.GetValue<int?>($"RateLimiting:{policy}")` ?? default (leitura 180, escrita 40, auth 8, pdf 30, midia 30). `x-cache` = `CachePolicy.HeaderValue` ou `"private, no-store"`.

- [ ] **Step 2: Rodar o teste 4**

Expected: PASS. Conferir manualmente que `/api/condicoes` traz `stale-while-revalidate=600`.

- [ ] **Step 3: Commit**

```bash
git add src/Fisga.Api/OpenApi tests/Fisga.Api.Tests/ContratoTests.cs
git commit -m "feat(fisga): rate limit e cache visíveis no contrato"
```

---

### Task 6: Contrato regerado e documentação sincronizada

**Files:**
- Modify: `Projetos/fisga/docs/api/openapi.json` (gerado)
- Modify: `Projetos/fisga/docs/api/inventario.md` (remover lacunas 2 e 6; renumerar; sem contagem mágica)
- Modify: `Projetos/fisga/docs/api/backlog.md` (F1 intro sem número)
- Modify: `Projetos/fisga/docs/api/README.md` (idem)
- Modify: `Projetos/fisga/docs/adr/0017-portal-publico-da-api.md` (as três ocorrências de número)
- Test: suíte completa

- [ ] **Step 1: Regenerar**

Run: `cd Projetos/fisga && ./scripts/gerar-openapi.sh`
Expected: `docs/api/openapi.json` atualizado; diff mostra `tags`, `operationId`, `security`, `x-*` em todas as operações.

- [ ] **Step 2: Atualizar inventário e textos**

- Remover as linhas 2 (security) e 6 (tags/`operationId`) da tabela de lacunas; renumerar 1..10.
- Trocar "as 12 lacunas"/"as lacunas medidas" por "as lacunas do contrato" nos quatro arquivos, mantendo número só se a frase precisar (o inventário é a fonte).
- Registrar em `inventario.md`, na seção de lacunas, que segurança, tag e `operationId` foram resolvidos no A1 (uma linha).

- [ ] **Step 3: Rodar tudo**

Run: `dotnet build Fisga.slnx -c Release -warnaserror && dotnet test Fisga.slnx -c Release --no-build`
Run: `npx --yes markdownlint-cli2@0.24.1` (na raiz do repo)
Expected: build e testes verdes; 0 issues de markdown.

- [ ] **Step 4: Commit**

```bash
git add docs/api src
git commit -m "docs(fisga): contrato regerado e inventário sem as lacunas 2 e 6"
```

---

## Verificação final do pacote

- [ ] `git diff main...HEAD --stat` limitado a `Projetos/fisga/**`.
- [ ] Nenhuma alteração em handler além de metadata (revisar diff: sem mudança de status, corpo ou header).
- [ ] `openapi.json` com 54 operações, 13 tags, 54 `operationId`, 50 `x-rate-limit`, 54 `x-cache`.
- [ ] PR fecha #62, #63 e #67; corpo lista o que mudou e como verificar.
