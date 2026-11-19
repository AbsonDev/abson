# Níveis de detalhe do local — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Substituir a privacidade binária do local por cinco níveis nomeados, com padrão `Cidade`, sem coletar coordenadas.

**Architecture:** Toda a decisão continua centralizada em `Pescaria.LocalPublico`; `ParaEntradaPublica` já é o único caminho para link, troféu e PDF. O plano só estende esse ponto, adiciona um campo opcional de rio/trecho e troca o padrão.

**Tech Stack:** .NET 10 / EF Core + Postgres (string-enum), Astro SSR, xUnit, Playwright.

**Spec:** `../specs/2026-11-19-niveis-de-detalhe-do-local-design.md`

## Global Constraints

- `PrivacidadeDoLocal` usa `HasConversion<string>().HasMaxLength(16)` → nomes com ≤16 chars; **não renomear** `Exato`, `Cidade`, `Oculto` (evita data migration).
- Valores numéricos: manter `Exato=0, Cidade=1, Oculto=2`; adicionar `Trecho=3, CorpoAgua=4`.
- `Local` ≤120; `CorpoOuTrecho` ≤80. Padrão `Cidade` em `DadosDaPescaria`, `EntradaDePescaria` e no `registrar.astro` (criação **e** edição).
- DTO público nunca expõe `CorpoOuTrecho` cru nem coordenada.
- Copy pt-BR, sem emoji, sem cor nova, alvo 44 px, label visível.
- Comandos: `export PATH="$HOME/.dotnet:$PATH"`; `dotnet restore Fisga.slnx --locked-mode`; `dotnet build Fisga.slnx -c Release --no-restore -warnaserror`; `dotnet test Fisga.slnx -c Release --no-build`; no web, `npm ci`, `npm run check`, `npm run build`.
- Ler `Projetos/fisga/AGENTS.md` e `docs/INDEX.md`; mudança de comportamento/contrato exige documentação no mesmo PR.

## Review Focus

| Risco | Onde é fixado |
|---|---|
| `Trecho`/`CorpoAgua` com campo vazio devem cair para `Cidade`, nunca para `Local` | Task 1 |
| `CorpoAgua` com vírgula deve mostrar só o trecho antes dela | Task 1 |
| `Exato` com `CorpoOuTrecho` preenchido não pode vazar o campo no DTO público | Task 3 |
| `Oculto` continua com `CidadePublica`/`EstadoPublico` nulos mesmo com campo preenchido | Task 1 |
| Linhas antigas com strings `Exato`/`Cidade`/`Oculto` seguem válidas sem migração de dados | Task 2 |

---

### Task 1: Domínio — níveis e regra de exibição

**Files:**
- Modify: `Projetos/fisga/src/Fisga.Domain/Pescarias/Enums.cs`
- Modify: `Projetos/fisga/src/Fisga.Domain/Pescarias/DadosDaPescaria.cs`
- Modify: `Projetos/fisga/src/Fisga.Domain/Pescarias/Pescaria.cs`
- Test: `Projetos/fisga/tests/Fisga.Domain.Tests/PescariaTests.cs`
- Test helper: `Projetos/fisga/tests/Fisga.Domain.Tests/Fabrica.cs` (se precisar de novo valor padrão)

**Interfaces:**
- Consumes: nada além do código atual.
- Produces: `PrivacidadeDoLocal.Trecho`, `PrivacidadeDoLocal.CorpoAgua`, `DadosDaPescaria.CorpoOuTrecho`, `Pescaria.CorpoOuTrecho`, `Pescaria.LocalPublico` com 5 níveis.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Privacidade_trecho_mostra_o_campo_inteiro()
{
    var dados = Fabrica.Dados() with
    {
        PrivacidadeDoLocal = PrivacidadeDoLocal.Trecho,
        CorpoOuTrecho = "Rio Sergipe, trecho centro"
    };
    Assert.Equal("Rio Sergipe, trecho centro", Fabrica.Pescaria(dados).LocalPublico);
}

[Fact]
public void Privacidade_corpo_agua_mostra_ate_a_primeira_virgula()
{
    var dados = Fabrica.Dados() with
    {
        PrivacidadeDoLocal = PrivacidadeDoLocal.CorpoAgua,
        CorpoOuTrecho = "Rio Sergipe, trecho centro"
    };
    Assert.Equal("Rio Sergipe", Fabrica.Pescaria(dados).LocalPublico);
}

[Fact]
public void Trecho_e_corpo_agua_sem_campo_caem_para_cidade()
{
    foreach (var nivel in new[] { PrivacidadeDoLocal.Trecho, PrivacidadeDoLocal.CorpoAgua })
    {
        var dados = Fabrica.Dados() with { PrivacidadeDoLocal = nivel, CorpoOuTrecho = "  " };
        Assert.Equal("Campinas · SP", Fabrica.Pescaria(dados).LocalPublico);
    }
}

[Fact]
public void Rejeita_corpo_ou_trecho_longo()
{
    var dados = Fabrica.Dados() with { CorpoOuTrecho = new string('a', 81) };
    var excecao = Assert.Throws<DomainException>(() => Fabrica.Pescaria(dados));
    Assert.Equal("corpo_ou_trecho_longo", excecao.Codigo);
}

[Fact]
public void Oculto_esconde_cidade_mesmo_com_campo_preenchido()
{
    var dados = Fabrica.Dados() with
    {
        PrivacidadeDoLocal = PrivacidadeDoLocal.Oculto,
        CorpoOuTrecho = "Rio Sergipe"
    };
    var pescaria = Fabrica.Pescaria(dados);
    Assert.Equal("Local não divulgado", pescaria.LocalPublico);
    Assert.Null(pescaria.CidadePublica);
    Assert.Null(pescaria.EstadoPublico);
}
```
- [ ] **Step 2: Run test to verify it fails**

Run: `cd Projetos/fisga && export PATH="$HOME/.dotnet:$PATH" && dotnet test Fisga.slnx -c Release --filter FullyQualifiedName~PescariaTests -v minimal`
Expected: FAIL com símbolos `Trecho`, `CorpoAgua`, `CorpoOuTrecho` inexistentes.

- [ ] **Step 3: Implement**

Adicionar os dois valores ao enum com os números fixados em Global Constraints; adicionar `CorpoOuTrecho` a `DadosDaPescaria`; em `Pescaria`, propriedade + atribuição em `Aplicar`, validação `corpo_ou_trecho_longo` e switch de `LocalPublico` com `Trecho` → campo inteiro, `CorpoAgua` → `Split(',')[0].Trim()`, fallback para `Cidade` quando o campo é branco. `CidadePublica`/`EstadoPublico` continuam nulos só em `Oculto`.
- [ ] **Step 4: Run test to verify it passes**

Run: o mesmo comando do Step 2.
Expected: PASS, incluindo os três testes existentes de `Exato`/`Cidade`/`Oculto`.
- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/src/Fisga.Domain/Pescarias Projetos/fisga/tests/Fisga.Domain.Tests
git commit -m "feat(fisga): cinco níveis de detalhe do local no domínio"
```

### Task 2: Persistência — coluna e migração

**Files:**
- Modify: `Projetos/fisga/src/Fisga.Infrastructure/Persistence/Configurations/PescariaConfiguration.cs`
- Create: `Projetos/fisga/src/Fisga.Infrastructure/Persistence/Migrations/20261119_NiveisDeDetalheDoLocal.cs` (+ `.Designer.cs` e snapshot gerados)
- Test: `Projetos/fisga/tests/Fisga.Api.Tests/PescariaConfigurationTests.cs` (novo; `Fisga.Api.Tests` referencia a Api, que referencia a Infrastructure)

**Interfaces:**
- Consumes: `Pescaria.CorpoOuTrecho`.
- Produces: coluna `fisga.pescarias."CorpoOuTrecho"` nullable.

- [ ] **Step 1: Write the failing test**

Teste de configuração que falha: verificar que `model.FindEntityType(typeof(Pescaria))!.FindProperty("CorpoOuTrecho")` tem `GetMaxLength() == 80` e `IsNullable`.
- [ ] **Step 2: Run test to verify it fails**

Run: `cd Projetos/fisga && export PATH="$HOME/.dotnet:$PATH" && dotnet test Fisga.slnx -c Release --filter FullyQualifiedName~PescariaConfigurationTests -v minimal`

Nota: o projeto de teste é `Fisga.Api.Tests`, que já referencia `Fisga.Api` → `Fisga.Infrastructure`; o teste pode usar `WebApplicationFactory` ou instanciar `FisgaDbContext` com provider SQLite em memória.
Expected: FAIL.
- [ ] **Step 3: Implement**

Mapear `builder.Property(p => p.CorpoOuTrecho).HasMaxLength(80)`; gerar migração com `dotnet ef migrations add NiveisDeDetalheDoLocal` a partir de `src/Fisga.Api` com `--project ../Fisga.Infrastructure`, conferindo que o `Up` só faz `AddColumn<string>` com `maxLength: 80`, `nullable: true`, `schema: "fisga"` — **sem UPDATE** nas linhas existentes.
- [ ] **Step 4: Run test to verify it passes**

Run: `cd Projetos/fisga && export PATH="$HOME/.dotnet:$PATH" && dotnet build Fisga.slnx -c Release --no-restore -warnaserror && dotnet test Fisga.slnx -c Release --filter FullyQualifiedName~PescariaConfigurationTests -v minimal`
Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/src/Fisga.Infrastructure/Persistence
git commit -m "feat(fisga): migração do campo corpo ou trecho"
```

### Task 3: Application — padrão e contrato público

**Files:**
- Modify: `Projetos/fisga/src/Fisga.Application/Pescarias/Dtos.cs`
- Test: `Projetos/fisga/tests/Fisga.Application.Tests/ServicoDePescariasTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: `EntradaDePescaria.CorpoOuTrecho`, `PrivacidadeDoLocal` default `Cidade`, `PescariaMapper.ParaEntradaPublica` com `CorpoOuTrecho = null`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Entrada_sem_privacidade_usa_cidade()
{
    var servico = new ServicoDePescarias(new PescariaRepositorioFake(),
        new RelogioFixo(Agora), new DispatcherFake());
    var salva = await servico.CriarAsync(Entrada(), UsuarioId);
    var detalhe = await servico.ObterDoUsuarioAsync(salva.Id, UsuarioId);
    Assert.Equal(PrivacidadeDoLocal.Cidade, detalhe.Entrada.PrivacidadeDoLocal);
}

[Fact]
public async Task Publico_de_exato_nao_devolve_o_campo_cru()
{
    var servico = new ServicoDePescarias(new PescariaRepositorioFake(),
        new RelogioFixo(Agora), new DispatcherFake());
    var salva = await servico.CriarAsync(Entrada() with
    {
        PrivacidadeDoLocal = PrivacidadeDoLocal.Exato,
        CorpoOuTrecho = "Poita do Zé"
    }, UsuarioId);
    var publica = await servico.ObterPublicoAsync(salva.ShareToken);
    Assert.Null(publica!.Entrada.CorpoOuTrecho);
    Assert.Equal("Represa do Lago Azul", publica.Entrada.Local);
}

[Fact]
public async Task Publico_de_corpo_agua_devolve_texto_resumido()
{
    var servico = new ServicoDePescarias(new PescariaRepositorioFake(),
        new RelogioFixo(Agora), new DispatcherFake());
    var salva = await servico.CriarAsync(Entrada() with
    {
        PrivacidadeDoLocal = PrivacidadeDoLocal.CorpoAgua,
        CorpoOuTrecho = "Rio Sergipe, trecho centro"
    }, UsuarioId);
    var publica = await servico.ObterPublicoAsync(salva.ShareToken);
    Assert.Equal("Rio Sergipe", publica!.Entrada.Local);
    Assert.Equal("CorpoAgua", publica.PrivacidadeDoLocal);
}
```
- [ ] **Step 2: Run test to verify it fails**

Run: `cd Projetos/fisga && export PATH="$HOME/.dotnet:$PATH" && dotnet test Fisga.slnx -c Release --filter FullyQualifiedName~ServicoDePescariasTests -v minimal`
Expected: FAIL.
- [ ] **Step 3: Implement**

Em `EntradaDePescaria`, `CorpoOuTrecho` + default `Cidade`; em `ParaDados` e `ParaEntrada`, propagar o campo; em `ParaEntradaPublica`, forçar `CorpoOuTrecho = null`.
- [ ] **Step 4: Run test to verify it passes**

Run: o mesmo comando do Step 2. Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/src/Fisga.Application/Pescarias Projetos/fisga/tests/Fisga.Application.Tests
git commit -m "feat(fisga): contrato público com níveis de detalhe"
```

### Task 4: Web — registro com seletor de nível

**Files:**
- Modify: `Projetos/fisga/web/src/lib/tipos.ts`
- Modify: `Projetos/fisga/web/src/pages/registrar.astro`
- Test: criar `Projetos/fisga/web/tests/niveis-de-detalhe.spec.ts` (suíte mockada padrão — `identity-e2e.spec.ts` é ignorado pelo `playwright.config.ts`)

**Interfaces:**
- Consumes: Task 3 (`EntradaDePescaria`).
- Produces: união `'Exato' | 'Trecho' | 'CorpoAgua' | 'Cidade' | 'Oculto'`, campo `corpoOuTrecho?: string | null`, elementos `#privacidade-local` e `#corpo-ou-trecho`.

- [ ] **Step 1: Write the failing test**

Teste E2E mockado: com `page.route` respondendo `/api/auth/me` como autenticado, abrir `/registrar`, assertar o radio `Cidade` marcado por padrão, selecionar `CorpoAgua`, preencher `#corpo-ou-trecho` com `Rio Sergipe, trecho centro`, submeter e **capturar o corpo do POST `/api/pescarias`**, afirmando `privacidadeDoLocal === 'CorpoAgua'` e `corpoOuTrecho === 'Rio Sergipe, trecho centro'`. Também afirmar que o campo aparece só para `Trecho`/`CorpoAgua` e que o helper muda com a seleção. A asserção do link público fica na Task 5 (rota SSR exige fixture no mock-api).
- [ ] **Step 2: Run test to verify it fails**

Run: `cd Projetos/fisga/web && npm run test:e2e -- --grep "nível de detalhe"`
Expected: FAIL.
- [ ] **Step 3: Implement**

Estender a união em `tipos.ts` e adicionar `corpoOuTrecho?: string | null`; no `registrar.astro`, substituir o `<select>` por cinco opções em pt-BR com `Cidade selected`, adicionar campo opcional "Rio, lagoa ou trecho" com placeholder "Ex.: Rio Sergipe, trecho centro", `maxlength="80"`, helper "Não informe o ponto exato.", visível só para `Trecho`/`CorpoAgua`; trocar **todos os três** defaults `'Exato'` por `'Cidade'` no JS: linha 766 (salvar), 785 (carregar edição) e 847 (`aplicarCondicoesDaUrl`, preenchimento vindo de `/hoje` — caminho real que hoje força `Exato` e viola "privado por padrão"). A UI deve seguir o artefato extraído do OpenDesign (tokens, espaçamento, copy).
- [ ] **Step 4: Run test to verify it passes**

Run: `cd Projetos/fisga/web && npm run check && npm run build && npm run test:e2e -- --grep "nível de detalhe"`
Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/web/src Projetos/fisga/web/tests
git commit -m "feat(fisga): seletor de nível de detalhe no registro"
```

### Task 5: Web — link público e troféu sem duplicação

**Files:**
- Modify: `Projetos/fisga/web/src/pages/p/[token].astro`
- Modify: `Projetos/fisga/web/src/pages/trofeu/[token]/[capturaId].astro`
- Modify: `Projetos/fisga/web/tests/mock-api.mjs` (fixture de `PescariaDetalhada` para tokens de teste — SSR não é alcançado por `page.route`)
- Modify: `Projetos/fisga/web/tests/niveis-de-detalhe.spec.ts` (criado na Task 4)

**Interfaces:**
- Consumes: `PescariaDetalhada.privacidadeDoLocal` e `entrada.local` já mascarado.

- [ ] **Step 1: Write the failing test**

Teste E2E mockado: adicionar em `tests/mock-api.mjs` uma fixture `PescariaDetalhada` com `privacidadeDoLocal: 'Cidade'`, `entrada.local: 'Campinas · SP'`, cidade/UF preenchidos e uma captura com ID estável; abrir `/p/<token-de-teste>` e assertar badge "Cidade aproximada" e **uma** ocorrência de "Campinas · SP" no cabeçalho; abrir `/trofeu/<token>/<capturaId>` e assertar a linha de local uma vez (hoje o troféu concatena `local · cidade · UF` e duplica). O mock-api já é o servidor de API do `playwright.config.ts`, usado pelo SSR.
- [ ] **Step 2: Run test to verify it fails**

Run: `cd Projetos/fisga/web && npm run test:e2e -- --grep "badge por nível"`
Expected: FAIL.
- [ ] **Step 3: Implement**

Em `/p/[token]`, trocar o ternário do badge por um mapa por nível (`Exato` sem badge, `Trecho` → "Trecho aproximado", `CorpoAgua` → "Rio/lagoa aproximado", `Cidade` → "Cidade aproximada", `Oculto` → "Local não divulgado"); no troféu, usar somente `entrada.local` como linha de local no card e no canvas, removendo a linha separada de cidade/UF. O PDF já lê `LocalPublico` — sem alteração.
- [ ] **Step 4: Run test to verify it passes**

Run: `cd Projetos/fisga/web && npm run check && npm run build && npm run test:e2e -- --grep "badge por nível"`
Expected: PASS; conferir manualmente o PDF gerado.
- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/web/src/pages
git commit -m "feat(fisga): badge por nível e troféu sem duplicar local"
```

### Task 6: Documentação e contrato — ADR, README e OpenAPI

**Files:**
- Create: `Projetos/fisga/docs/adr/0015-niveis-de-detalhe-do-local.md`
- Modify: `Projetos/fisga/docs/adr/0008-privacidade-local-e-fotos.md` (status + nota de supersedência **só** do item "três valores"; as decisões de fotos permanecem)
- Modify: `Projetos/fisga/docs/adr/README.md`
- Modify: `Projetos/fisga/README.md` (linha ~79)
- Modify: `Projetos/fisga/docs/api/openapi.json` (**regenerado**, nunca editado à mão)

**Interfaces:**
- Consumes: Tasks 1–3 (enum `CorpoAgua`/`Trecho`, `corpoOuTrecho` no DTO de entrada, default `Cidade`) e Task 5 (rótulos de badge).
- Produces: documentação coerente com o código e `openapi.json` idêntico ao gerado pelo CI.

- [ ] **Step 1: Escrever a ADR 0015**

Status `aceito`, data 2026-11-19. Contexto: o link público mostrava o ponto exato por padrão. Decisão: cinco níveis nomeados (`Exato`, `Trecho`, `CorpoAgua`, `Cidade`, `Oculto`), padrão `Cidade`, campo opcional `CorpoOuTrecho` (≤80) nunca devolvido cru no DTO público, sem raio em metros e sem coordenada nova. Consequências: fallback para `Cidade` quando o campo está vazio; PDF/troféu/link usam a mesma projeção; upvote/raio ficam para fase posterior. Referenciar a spec e o draft privado do quadro.

- [ ] **Step 2: Atualizar a documentação existente**

Em `0008`, marcar o status como `substituído pelo [ADR 0015](0015-niveis-de-detalhe-do-local.md)` e deixar claro que apenas a decisão dos três valores foi substituída. Adicionar a linha de `0015` na tabela de `docs/adr/README.md`. Em `README.md:79`, trocar por "exato, trecho, rio/lagoa, cidade/UF ou oculto — padrão cidade/UF".

- [ ] **Step 3: Regenerar o contrato**

Run: `cd Projetos/fisga && export PATH="$HOME/.dotnet:$PATH" && bash scripts/gerar-openapi.sh`
Conferir que o diff de `docs/api/openapi.json` contém os novos valores do enum e o campo `corpoOuTrecho` no schema de entrada, e nada mais. Nunca editar o JSON na mão.

- [ ] **Step 4: Verificar e commitar**

Run: `npx --yes markdownlint-cli2@0.24.1`, `dotnet build Fisga.slnx -c Release --no-restore -warnaserror`, `dotnet test Fisga.slnx -c Release --no-build`.

```bash
git add Projetos/fisga/docs Projetos/fisga/README.md
git commit -m "docs(fisga): ADR dos cinco níveis de detalhe e contrato regenerado"
```

---

## Fim do plano

`dotnet test Fisga.slnx -c Release --no-build` + `npm run check` + `npm run build` + E2E + `npx --yes markdownlint-cli2@0.24.1` + `bash scripts/gerar-openapi.sh` com diff limpo. Nenhum deploy automático antes da revisão final.
