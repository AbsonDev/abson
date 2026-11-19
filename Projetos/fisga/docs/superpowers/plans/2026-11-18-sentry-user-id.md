# Sentry User Id Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer o Sentry contar `usersImpacted` sem enviar PII, preservando apenas um Guid opaco.

**Architecture:** Um helper puro converte o `Guid` do usuário em `SentryUser` com só o `Id`. O `SessaoIdentityMiddleware` define esse usuário no escopo do Sentry após validar a sessão. O `SentryScrubber` deixa de apagar `User` e passa a remover apenas os campos de PII, em eventos e transações.

**Tech Stack:** C# / .NET 10, Sentry .NET SDK 7.0.1, xUnit 2.9.3.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-18-sentry-user-id-design.md`

## Global Constraints

- Antes de qualquer comando `dotnet`: `export PATH="$HOME/.dotnet:$PATH"`.
- Todos os comandos a partir de `Projetos/fisga`.
- Teste filtrado: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~<Classe>"`.
- Convenção de teste: xUnit com fakes manuais; **sem** biblioteca de mock.
- Comentários, nomes e mensagens em português do Brasil.
- Não tocar no PR #33, nos autofixes do Seer nem em issues do Sentry.
- Não alterar `SendDefaultPii = false` nem os filtros de query/URL/mensagem do scrubber.

## Review Focus

- Usuário anônimo: o evento deve continuar sem `User` — teste `ScrubEvent_mantem_sem_usuario_quando_anonimo`.
- PII no evento: `Email`, `Username`, `IpAddress`, `Other` devem ser removidos — teste `ScrubEvent_preserva_id_e_remove_pii_do_usuario`.
- PII na transação: mesma limpeza — teste `ScrubTransaction_preserva_id_e_remove_pii_do_usuario`.
- Id exato: o Guid original deve sobreviver ao scrubber — asserção nos dois testes de limpeza.
- Helper sem PII: `IdentidadeSentry.ParaUsuario` recebe só `Guid` — teste `ParaUsuario_gera_id_sem_outros_campos`.

---

### Task 1: helper de identidade opaca

**Files:**
- Create: `src/Fisga.Api/Observabilidade/IdentidadeSentry.cs`
- Test: `tests/Fisga.Api.Tests/IdentidadeSentryTests.cs`

**Interfaces:**
- Consumes: `System.Guid`, `Sentry.SentryUser`.
- Produces: `IdentidadeSentry.ParaUsuario(Guid usuarioId) -> SentryUser` com apenas `Id = usuarioId.ToString()`.

- [ ] **Step 1: escrever o teste que falha**

```csharp
[Fact]
public void ParaUsuario_gera_id_sem_outros_campos()
{
    var id = Guid.NewGuid();

    var usuario = IdentidadeSentry.ParaUsuario(id);

    Assert.Equal(id.ToString(), usuario.Id);
    Assert.Null(usuario.Email);
    Assert.Null(usuario.Username);
    Assert.Null(usuario.IpAddress);
    Assert.Empty(usuario.Other);
}
```

- [ ] **Step 2: rodar o teste e confirmar que falha**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~IdentidadeSentryTests"`
Expected: FAIL com erro de compilação — `IdentidadeSentry` não existe.

- [ ] **Step 3: implementar o helper**

Em `src/Fisga.Api/Observabilidade/IdentidadeSentry.cs`, namespace `Fisga.Api.Observabilidade`,
classe `public static class IdentidadeSentry` com o método
`public static SentryUser ParaUsuario(Guid usuarioId)`. O corpo devolve `new SentryUser { Id = usuarioId.ToString() }`.

- [ ] **Step 4: rodar o teste e confirmar que passa**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~IdentidadeSentryTests"`
Expected: PASS.

- [ ] **Step 5: commit**

```bash
git add src/Fisga.Api/Observabilidade/IdentidadeSentry.cs tests/Fisga.Api.Tests/IdentidadeSentryTests.cs
git commit -m "feat(obs): helper de identidade opaca para o Sentry"
```

### Task 2: scrubber preserva o Id e remove PII

**Files:**
- Modify: `src/Fisga.Api/Observabilidade/SentryScrubber.cs:54-85`
- Test: `tests/Fisga.Api.Tests/SentryScrubberTests.cs`

**Interfaces:**
- Consumes: `Sentry.SentryEvent`, `Sentry.SentryTransaction`, `Sentry.SentryUser`.
- Produces: `ScrubEvent` e `ScrubTransaction` continuam com as assinaturas atuais; `User` passa a ser limpo em vez de anulado.

- [ ] **Step 1: escrever os três testes que falham**

Em `SentryScrubberTests.cs`, adicionar:

```csharp
[Fact]
public void ScrubEvent_preserva_id_e_remove_pii_do_usuario()
{
    var evento = new SentryEvent
    {
        User = new SentryUser
        {
            Id = "usuario-123",
            Email = "alguem@exemplo.com",
            Username = "alguem",
            IpAddress = "192.0.2.1",
            Other = new Dictionary<string, string> { ["plano"] = "pro" }
        }
    };

    var limpo = SentryScrubber.ScrubEvent(evento, new SentryHint());

    Assert.Equal("usuario-123", limpo!.User!.Id);
    Assert.Null(limpo.User.Email);
    Assert.Null(limpo.User.Username);
    Assert.Null(limpo.User.IpAddress);
    Assert.Empty(limpo.User.Other);
}

[Fact]
public void ScrubEvent_mantem_sem_usuario_quando_anonimo()
{
    var limpo = SentryScrubber.ScrubEvent(new SentryEvent(), new SentryHint());

    Assert.Null(limpo!.User);
}

[Fact]
public void ScrubTransaction_preserva_id_e_remove_pii_do_usuario()
{
    var transacao = new SentryTransaction("GET /api/x")
    {
        User = new SentryUser { Id = "usuario-456", Email = "alguem@exemplo.com" }
    };

    var limpa = SentryScrubber.ScrubTransaction(transacao, new SentryHint());

    Assert.Equal("usuario-456", limpa!.User!.Id);
    Assert.Null(limpa.User.Email);
}
```

- [ ] **Step 2: rodar os testes e confirmar que falham pelo motivo certo**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~SentryScrubberTests"`
Expected: FAIL nas três novas asserções — `User` ainda é anulado (`Assert.Null` do Id falha).

- [ ] **Step 3: implementar `LimparUsuario` e trocar as duas atribuições**

Em `SentryScrubber.cs`:
- adicionar `private static void LimparUsuario(SentryUser? usuario)` que retorna cedo se nulo e
  zera `Email`, `Username`, `IpAddress` e limpa `Other`, mantendo `Id`;
- em `ScrubEvent`, trocar `evento.User = null!;` por `LimparUsuario(evento.User);`;
- em `ScrubTransaction`, trocar `transacao.User = null!;` por `LimparUsuario(transacao.User);`.

Não alterar `LimparRequest`, `LimparMensagens`, `ScrubUrl`, `ScrubQuery` nem `ScrubText`.

- [ ] **Step 4: rodar os testes e confirmar que passam**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~SentryScrubberTests"`
Expected: PASS nos 7 testes da classe.

- [ ] **Step 5: commit**

```bash
git add src/Fisga.Api/Observabilidade/SentryScrubber.cs tests/Fisga.Api.Tests/SentryScrubberTests.cs
git commit -m "fix(obs): preservar id opaco e remover pii do usuario no sentry"
```

### Task 3: instrumentar o middleware e validar a suíte

**Files:**
- Modify: `src/Fisga.Api/Middleware/SessaoIdentityMiddleware.cs:65-69`

**Interfaces:**
- Consumes: `IdentidadeSentry.ParaUsuario(Guid) -> SentryUser` (Task 1), `usuario.Id` do objeto de domínio.
- Produces: escopo do Sentry com `User.Id` preenchido para requisições autenticadas.

- [ ] **Step 1: adicionar `using Sentry;` e a chamada no middleware**

Logo após `context.DefinirSessao(sessao, usuario);` dentro de `if (usuario is not null)`:

```csharp
SentrySdk.ConfigureScope(escopo =>
    escopo.User = IdentidadeSentry.ParaUsuario(usuario.Id));
```

Não mover a chamada para fora do `if` e não setar `User` em usuário anônimo.

- [ ] **Step 2: buildar com avisos como erro**

Run: `dotnet build Fisga.slnx -c Release --warnaserror`
Expected: BUILD SUCCESSED, zero avisos.

- [ ] **Step 3: rodar a suíte completa da API**

Run: `dotnet test Fisga.slnx -c Release`
Expected: PASS — todos os testes existentes e os 4 novos.

- [ ] **Step 4: commit**

```bash
git add src/Fisga.Api/Middleware/SessaoIdentityMiddleware.cs
git commit -m "feat(obs): identificar usuario no sentry apos validar sessao"
```
