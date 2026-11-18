# Fisga Identity 2A + Google + E-mail/Senha — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar a autenticação do Fisga para ASP.NET Core Identity completo, com Google, e-mail/senha, link mágico permanente, consentimento formal e controle de dispositivos, mantendo o diário no PostgreSQL.

**Architecture:** `ApplicationUser : IdentityUser<Guid>` compartilha o mesmo Id do `Usuario` de domínio. O cookie da Identity (`fisga_auth`) é a única autenticação; a tabela `Sessao` vira registro de dispositivo/revogação via claim `sessao_id`. Google usa `AddGoogle`; e-mail/senha usa `UserManager`/`SignInManager`; o link mágico continua e faz sign-in pela Identity.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core 10, PostgreSQL, Google OAuth/OpenID Connect, Astro 7, Playwright.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-17-identity-google-design.md`

## Global Constraints

- Backend continua .NET 10 + PostgreSQL; frontend continua Astro 7.
- Cookie `fisga_auth`: `HttpOnly`, `Secure` em produção, `SameSite=Lax`, 30 dias.
- Senha: mínimo 10, máximo 128, sem exigir maiúscula/número/símbolo; lockout 5 tentativas/15 min.
- Login por senha exige e-mail confirmado; Google e link mágico também escrevem o estado verificado.
- Google auto-link só com e-mail Google verificado **e** conta local já verificada.
- “Conectar Google” exige e-mail Google verificado igual ao e-mail da conta; nunca troca a conta.
- Consentimento formal de Termos/Privacidade é obrigatório antes de acessar dados.
- Migration aditiva; rollback oficial é a imagem anterior, não restore destrutivo.
- Sem telefone, Firebase, Firestore, 2FA ou passkeys nesta entrega.
- Nenhuma senha, token, cookie ou e-mail em Sentry/logs/métricas.
- Data Protection keys persistidas em volume/configuração para o cookie sobreviver a restart/deploy.
- Google exige `Google:ClientId`/`Google:ClientSecret` e redirects autorizados; sem isso, o botão fica oculto e o login Google indisponível.
- TDD: teste falhando antes de cada implementação; commits pequenos por tarefa.

## Review Focus

- Usuário antigo com Google de e-mail diferente não pode perder o diário nem trocar de conta.
- Auto-link em conta local não verificada não pode permitir pré-sequestro.
- Sessão revogada não pode continuar aceita pelo cookie da Identity.
- Consentimento pendente não pode ser contornado chamando a API direto.
- “Conectar Google” não pode trocar a conta autenticada nem vincular a outro usuário.
- Token de reset/confirmação não pode ser reutilizado, expirado ou aceito para outro usuário.

---

### Task 1: Identity schema, ApplicationUser e Consentimento

**Files:**
- Create: `tests/Fisga.Api.Tests/Fisga.Api.Tests.csproj`
- Create: `tests/Fisga.Api.Tests/TestWebApplicationFactory.cs`
- Create: `tests/Fisga.Api.Tests/IdentitySchemaTests.cs`
- Create: `src/Fisga.Infrastructure/Identity/ApplicationUser.cs`
- Create: `src/Fisga.Domain/Identidade/TipoDeConsentimento.cs`
- Create: `src/Fisga.Domain/Identidade/Consentimento.cs`
- Create: `src/Fisga.Application/Identidade/IConsentimentoRepository.cs`
- Create: `src/Fisga.Infrastructure/Persistence/Configurations/ConsentimentoConfiguration.cs`
- Create: `src/Fisga.Infrastructure/Persistence/Repositories/EfConsentimentoRepository.cs`
- Modify: `src/Fisga.Infrastructure/Persistence/FisgaDbContext.cs`
- Modify: `src/Fisga.Infrastructure/Fisga.Infrastructure.csproj`
- Modify: `Fisga.slnx`
- Create: migration em `src/Fisga.Infrastructure/Persistence/Migrations/`

**Interfaces:**
- Produces: `ApplicationUser : IdentityUser<Guid>`, `TipoDeConsentimento`, `Consentimento`, `IConsentimentoRepository` (em `Fisga.Application.Identidade`, com `ObterUltimoAsync`, `TemVersaoAsync`, `Adicionar`), `EfConsentimentoRepository`, `FisgaDbContext` herdando `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.

- [ ] **Step 1: Criar o projeto de testes de API e o teste de schema**

`Fisga.Api.Tests.csproj` deve referenciar `Fisga.Api`, `Microsoft.AspNetCore.Mvc.Testing` 10.0.15 e `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15. O teste:

```csharp
[Fact]
public async Task Banco_cria_tabelas_da_identity_e_consentimentos()
{
    await using var factory = new TestWebApplicationFactory();
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
    await db.Database.EnsureCreatedAsync();

    var tabelas = await db.Database
        .SqlQueryRaw<string>("select name as Value from sqlite_master where type = 'table'")
        .ToListAsync();

    Assert.Contains("AspNetUsers", tabelas);
    Assert.Contains("consentimentos", tabelas);
}
```

`TestWebApplicationFactory` deve substituir `Database:Provider` por `Sqlite` e apontar para um arquivo temporário, além de substituir o `IEmailSender` por um fake.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests/Fisga.Api.Tests.csproj --filter Banco_cria_tabelas`
Expected: FAIL porque `ApplicationUser`/`consentimentos` ainda não existem.

- [ ] **Step 3: Implementar ApplicationUser, Consentimento e DbContext**

`ApplicationUser` vazio, herdando `IdentityUser<Guid>`. `Consentimento` com `Id`, `UsuarioId`, `Tipo` (`TipoDeConsentimento.Termos`/`Privacidade`), `Versao`, `AceitoEm`; factory `Criar` valida campos e versão não vazia. `FisgaDbContext` herda `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`, chama `base.OnModelCreating`, define `HasDefaultSchema("fisga")` para as tabelas da Identity, adiciona `DbSet<Consentimento> Consentimentos` e mantém os DbSets atuais. `EfConsentimentoRepository` implementa a interface.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests/Fisga.Api.Tests.csproj --filter Banco_cria_tabelas`
Expected: PASS.

- [ ] **Step 5: Criar a migration aditiva**

Run: `dotnet ef migrations add IdentityConsentimento --project src/Fisga.Infrastructure --startup-project src/Fisga.Api --output-dir Persistence/Migrations`
Expected: migration cria `AspNetUsers`, tabelas de Identity e `consentimentos`; não dropa nem renomeia tabelas atuais.

- [ ] **Step 6: Commit**

```bash
git add tests/Fisga.Api.Tests src/Fisga.Infrastructure src/Fisga.Domain/Identidade src/Fisga.Application/Identidade/IConsentimentoRepository.cs
git commit -m "feat(auth): add identity schema and consent entity"
```

---

### Task 2: Identity, cookie e opções de segurança

**Files:**
- Create: `src/Fisga.Infrastructure/Identity/IdentityExtensions.cs`
- Modify: `src/Fisga.Infrastructure/DependencyInjection.cs`
- Modify: `src/Fisga.Api/Program.cs`
- Modify: `src/Fisga.Api/appsettings.json`
- Modify: `src/Fisga.Infrastructure/Fisga.Infrastructure.csproj`
- Create: `tests/Fisga.Api.Tests/IdentityOptionsTests.cs`

**Interfaces:**
- Produces: `IdentityExtensions.AdicionarIdentity(IServiceCollection, IConfiguration)`; cookie `fisga_auth`; opções de senha/lockout/email.
- Consumes: `FisgaDbContext` da Task 1.

- [ ] **Step 1: Escrever o teste de opções**

```csharp
[Collection("api")]
public sealed class IdentityOptionsTests(ApiTestFixture fixture)
{
    [Fact]
    public void Identity_usa_cookie_e_politicas_do_spec()
    {
        var identity = fixture.Factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var cookie = fixture.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("fisga_auth", cookie.Cookie.Name);
        Assert.True(identity.SignIn.RequireConfirmedEmail);
        Assert.Equal(10, identity.Password.RequiredLength);
        Assert.False(identity.Password.RequireNonAlphanumeric);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identity.Lockout.DefaultLockoutTimeSpan);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter Identity_usa_cookie`
Expected: FAIL (cookie padrão/opções ausentes).

- [ ] **Step 3: Implementar**

Adicionar `Microsoft.AspNetCore.Authentication.Google` 10.0.15. Em `IdentityExtensions`, usar `AddIdentity<ApplicationUser, IdentityRole<Guid>>()`, `AddEntityFrameworkStores<FisgaDbContext>()`, `AddDefaultTokenProviders()`, configurar `IdentityOptions` e `ConfigureApplicationCookie` com `fisga_auth`. Adicionar `AddGoogle(...)` **somente quando** `Google:ClientId` e `Google:ClientSecret` estiverem preenchidos; usar `SignInScheme = IdentityConstants.ExternalScheme`, `CallbackPath = "/signin-google"` e `SaveTokens = true`. Configurar `DataProtection` com `PersistKeysToFileSystem` usando `DataProtection:KeysPath` (default `data/keys`). Registrar em `DependencyInjection`. Em `Program`, chamar `app.UseAuthentication()` antes do middleware de sessão.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter Identity_usa_cookie`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Infrastructure/Identity src/Fisga.Infrastructure/DependencyInjection.cs src/Fisga.Api/Program.cs src/Fisga.Api/appsettings.json tests/Fisga.Api.Tests
git commit -m "feat(auth): configure ASP.NET Core Identity and Google"
```

---

### Task 3: Provisionamento de conta e cadastro por senha

**Files:**
- Create: `src/Fisga.Application/Identidade/ICadastroDeUsuario.cs`
- Create: `src/Fisga.Infrastructure/Identity/CadastroDeUsuario.cs`
- Modify: `src/Fisga.Application/Identidade/Contratos.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Create: `tests/Fisga.Api.Tests/CadastroDeUsuarioTests.cs`
- Create: `tests/Fisga.Api.Tests/RegistroTests.cs`

**Interfaces:**
- Produces: `ICadastroDeUsuario` em Application; `CadastroDeUsuario : ICadastroDeUsuario` em Infrastructure com `GarantirUsuarioAsync(Email, string?, bool, CancellationToken) : Task<Usuario>`, `CriarComSenhaAsync(string email, string? nome, string senha, CancellationToken) : Task<Usuario>`, `GarantirApplicationUserAsync(Usuario, CancellationToken) : Task<ApplicationUser>`, `ObterUsuarioAsync(Guid, CancellationToken)`.
- Produces: `POST /api/auth/registrar` com `{ nome, email, senha }`.

- [ ] **Step 1: Escrever testes de provisionamento**

Testes: criar conta por senha cria `Usuario` e `ApplicationUser` com o mesmo Id; e-mail duplicado não cria segundo usuário; senha curta (<10) e senha longa (>128) são rejeitadas; `Usuario.EmailVerificadoEm` começa nulo.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter CadastroDeUsuario`
Expected: FAIL.

- [ ] **Step 3: Implementar provisionamento transacional**

`CadastroDeUsuario` usa `UserManager<ApplicationUser>` + `FisgaDbContext` + `IUsuarioRepository` + `IUnitOfWork` + `IDomainEventDispatcher`. Validar `senha.Length` entre 10 e 128 antes de `CreateAsync` (a Identity não tem máximo nativo). Abrir transação, criar `Usuario`, criar `ApplicationUser` com o mesmo Id/Email/UserName, `CreateAsync`, salvar, publicar eventos, commit. Em falha, rollback e remover o usuário de domínio do contexto.

- [ ] **Step 4: Implementar endpoint de registro**

`POST /api/auth/registrar` valida entrada, chama `CriarComSenhaAsync`, gera token de confirmação e envia e-mail. Resposta 201 sem revelar hash/token. Rate limit `auth`.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter "CadastroDeUsuario|Registro"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Fisga.Infrastructure/Identity/CadastroDeUsuario.cs src/Fisga.Application/Identidade/Contratos.cs src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): add transactional account provisioning and password registration"
```

---

### Task 4: Login por senha, confirmação de e-mail e reset

**Files:**
- Create: `src/Fisga.Infrastructure/Emails/EmailDeIdentidade.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Create: `tests/Fisga.Api.Tests/LoginSenhaTests.cs`
- Create: `tests/Fisga.Api.Tests/ConfirmacaoResetTests.cs`

**Interfaces:**
- Produces: `POST /api/auth/entrar`, `POST /api/auth/confirmar-email`, `POST /api/auth/esqueci-senha`, `POST /api/auth/redefinir-senha`.
- Consumes: `CadastroDeUsuario` e `UserManager`/`SignInManager` da Task 3.

- [ ] **Step 1: Escrever testes**

Cobrir: login válido emite cookie `fisga_auth`; e-mail não confirmado não entra; senha errada incrementa lockout; confirmação válida muda `EmailConfirmed` e `Usuario.EmailVerificadoEm`; reset válido troca a senha e invalida o token; token de reset reutilizado falha.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter "LoginSenha|ConfirmacaoReset"`
Expected: FAIL.

- [ ] **Step 3: Implementar login e confirmação**

`POST /api/auth/entrar` usa `SignInManager.PasswordSignInAsync`; em sucesso, o próprio Identity emite o cookie `fisga_auth`. A sessão de dispositivo/claim `sessao_id` será adicionada na Task 5; não crie `IdentitySessionService` agora. `POST /api/auth/confirmar-email` usa `UserManager.ConfirmEmailAsync`, atualiza `Usuario.VerificarEmail` e salva. `RequireConfirmedEmail=true` já está na Task 2.

- [ ] **Step 4: Implementar reset**

`POST /api/auth/esqueci-senha` sempre responde 202; se o usuário existir, gera token e envia link para a web. `POST /api/auth/redefinir-senha` usa `ResetPasswordAsync`, salva e invalida sessões existentes.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter "LoginSenha|ConfirmacaoReset"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Fisga.Infrastructure/Emails src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): add password login, email confirmation and reset"
```

---

### Task 5: Ponte de sessão e dispositivos

**Files:**
- Modify: `src/Fisga.Domain/Identidade/Sessao.cs`
- Modify: `src/Fisga.Application/Identidade/Contratos.cs`
- Modify: `src/Fisga.Infrastructure/Persistence/Repositories/EfSessaoRepository.cs`
- Create: `src/Fisga.Api/IdentitySessionService.cs`
- Create: `src/Fisga.Api/Middleware/SessaoIdentityMiddleware.cs`
- Modify: `src/Fisga.Api/Middleware/SessaoMiddleware.cs`
- Modify: `src/Fisga.Api/Program.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Create: `tests/Fisga.Api.Tests/SessaoIdentityTests.cs`
- Create: `tests/Fisga.Domain.Tests/SessaoTests.cs` (se ainda não existir)

**Interfaces:**
- Produces: `Sessao.Criar(Guid id, Guid usuarioId, string tokenHash, string? userAgent, string? ipHash, DateTimeOffset agora, TimeSpan duracao)`; o overload atual delega com `Guid.NewGuid()`.
- Produces: `ISessaoRepository.ObterPorIdAsync(Guid, CancellationToken)`; implementação EF com `FirstOrDefaultAsync(s => s.Id == id)`.
- Produces: `IdentitySessionService.AssinarAsync(ApplicationUser conta, Usuario usuario, HttpContext http, CancellationToken ct) : Task`.
- Produces: claim `sessao_id` no cookie da Identity; `SessaoIdentityMiddleware`.
- Consumes: `SignInManager`/Identity da Task 2 e `Sessao`/`ISessaoRepository` atuais.

**Decisões de implementação (vinculantes):**
- Não haverá cache da validação de sessão nesta entrega. O middleware consulta a sessão por chave primária a cada requisição `/api`. `UltimoUsoEm` só é atualizado quando passou mais de 5 minutos, como hoje.
- O login por senha passa a usar `SignInManager.CheckPasswordSignInAsync(conta, senha, lockoutOnFailure: true)` e, em sucesso, `IdentitySessionService.AssinarAsync`. Não chamar `PasswordSignInAsync` e assinar de novo.
- O `SessaoMiddleware` antigo (`fisga_sessao`) permanece como fallback somente quando a Identity ainda não resolveu a sessão. A Task 6 remove o fallback ao migrar o link mágico. Isso mantém a suíte verde entre as tarefas.
- “Sair de todos” invalida imediatamente porque revoga todas as linhas de `Sessoes`; o `SecurityStamp` é defesa em profundidade, não o mecanismo primário.
- Sessão cujo `UsuarioId` não bate com o `NameIdentifier` do cookie é tratada como inválida e não define `Items`.

- [ ] **Step 1: Escrever testes**

Cobrir:
1. Login válido cria uma linha em `Sessoes` com `UsuarioId` do usuário e `TokenHash` de 64 caracteres; `/api/auth/me` retorna autenticado.
2. Sessão revogada diretamente no banco faz `/api/auth/me` retornar `autenticado:false` e um endpoint protegido (`/api/conta/sessoes`) retornar 401; o cookie é limpo.
3. Sessão expirada (`ExpiraEm` no passado) é tratada como inválida.
4. Claim `sessao_id` apontando para sessão de outro usuário não autentica.
5. `POST /api/auth/sair` revoga apenas a sessão atual e limpa o cookie; outro cliente continua autenticado.
6. `POST /api/auth/sair-de-todos` revoga todas as sessões do usuário; o segundo cliente perde acesso no request seguinte.
7. `GET /api/conta/sessoes` marca a sessão atual (`atual: true` em exatamente uma).

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter SessaoIdentity`
Expected: FAIL.

- [ ] **Step 3: Implementar domínio e repositório**

Adicionar o overload de `Sessao.Criar` que recebe `Id`; validar `Id != Guid.Empty`. Extrair a validação de `tokenHash`/duração para um método privado para não duplicar. Adicionar `ObterPorIdAsync` à interface e ao repositório EF.

- [ ] **Step 4: Implementar assinatura e middleware**

`IdentitySessionService`:
- gera `var id = Guid.NewGuid()`;
- cria `Sessao.Criar(id, usuario.Id, TokensSeguros.Hash(id.ToString()), userAgent, ipHash, relogio.Agora, TimeSpan.FromDays(opcoes.DuracaoDaSessaoEmDias))`;
- adiciona ao repositório e `IUnitOfWork.SalvarAsync`;
- chama `signIn.SignInWithClaimsAsync(conta, new AuthenticationProperties { IsPersistent = true }, [new Claim("sessao_id", id.ToString())])`;
- publica `usuario.Eventos` + `new SessaoIniciada(usuario.Id, sessao.Id, agora)` via `IDomainEventDispatcher`, depois `usuario.LimparEventos()` (mesmo padrão de `ServicoDeAutenticacao.ConfirmarAsync`);
- chama `http.DefinirSessao(sessao, usuario)`.

`SessaoIdentityMiddleware`, para `/api`:
- se não houver `HttpContext.User` autenticado, segue;
- lê `sessao_id`; sem claim → segue (fallback legado ainda pode resolver);
- busca por id; se nula, revogada, expirada ou `UsuarioId` diferente do `NameIdentifier` → `SignOutAsync(IdentityConstants.ApplicationScheme)`, não define `Items`, segue;
- se válida e `agora - UltimoUsoEm > 5 min` → `RegistrarUso` + salvar;
- carrega `Usuario` por id (`IUsuarioRepository`) e chama `http.DefinirSessao`.
- O middleware precisa tolerar `Usuario` ausente sem derrubar a requisição.
- Nos testes de API, limpar `Sessoes` antes de `Usuarios`/`Users` (o FK de `sessoes` para `usuarios` é cascade, mas a ordem explícita evita depender disso).

Em `Program.cs`, registrar `IdentitySessionService` como scoped e usar `app.UseAuthentication()` seguido de `app.UseMiddleware<SessaoIdentityMiddleware>()` antes do `SessaoMiddleware` legado.

- [ ] **Step 5: Implementar logout**

`POST /api/auth/sair`:
- se autenticado e com `sessao_id`, revogar essa `Sessao` (`Revogar` + salvar);
- `SignOutAsync(IdentityConstants.ApplicationScheme)`;
- limpar o cookie legado `fisga_sessao`;
- 204.

`POST /api/auth/sair-de-todos`:
- `sessoes.RevogarTodasAsync(usuarioId, agora, exceto: null)`;
- `UserManager.UpdateSecurityStampAsync(conta)`;
- `SignOutAsync` + limpar cookie legado;
- 204.

- [ ] **Step 6: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter SessaoIdentity`
Expected: PASS.

- [ ] **Step 7: Rodar a suíte completa**

Run: `dotnet test`
Expected: PASS, incluindo os testes antigos de link mágico (fallback legado).

- [ ] **Step 8: Commit**

```bash
git add src/Fisga.Domain/Identidade/Sessao.cs src/Fisga.Application/Identidade/Contratos.cs src/Fisga.Infrastructure/Persistence/Repositories/EfSessaoRepository.cs src/Fisga.Api tests/Fisga.Api.Tests tests/Fisga.Domain.Tests
git commit -m "feat(auth): bridge identity cookie to device sessions"
```

---

### Task 6: Link mágico usando a Identity

**Files:**
- Modify: `src/Fisga.Application/Identidade/ServicoDeAutenticacao.cs`
- Modify: `src/Fisga.Application/Identidade/Contratos.cs`
- Modify: `src/Fisga.Infrastructure/Identity/CadastroDeUsuario.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Delete: `src/Fisga.Api/Middleware/SessaoMiddleware.cs`
- Modify: `src/Fisga.Api/Program.cs`
- Create: `tests/Fisga.Api.Tests/LinkMagicoIdentityTests.cs`
- Modify: `tests/Fisga.Application.Tests/ServicoDeAutenticacaoTests.cs`
- Modify: `tests/Fisga.Api.Tests/CadastroDeUsuarioTests.cs`

**Interfaces:**
- Produces: `ResultadoDaConfirmacao(Usuario Usuario, bool PrimeiroAcesso)` (substitui `SessaoCriada`).
- Modifies: `ICadastroDeUsuario.GarantirUsuarioAsync` passa a garantir **também** o `ApplicationUser` com o mesmo Id e `EmailConfirmed = emailVerificado`, na mesma transação (o XML doc da interface já promete isso; a implementação atual só cria o `Usuario`).
- Consumes: `IdentitySessionService.AssinarAsync` da Task 5 e `UserManager<ApplicationUser>`.

**Decisões de implementação (vinculantes):**
- Remover o middleware legado `SessaoMiddleware` do pipeline e deletar o arquivo. Não podem existir dois sistemas de sessão ao mesmo tempo.
- `ServicoDeAutenticacao` deixa de criar `Sessao`, token de cookie, `ValidarSessaoAsync` e `SairAsync`. Mantém `SolicitarLinkAsync`, `ConfirmarAsync` e `ListarSessoesAsync`.
- O endpoint `/api/auth/confirmar` carrega o `ApplicationUser` por id via `UserManager`, chama `IdentitySessionService.AssinarAsync(conta, usuario, http, ct)` e redireciona para o mesmo destino de hoje.
- `DefinirCookie` (que escrevia `fisga_sessao`) sai; `LimparCookie` permanece apenas como limpeza de cookie obsoleto no logout, sem gravar cookie novo.
- `GarantirUsuarioAsync` com `emailVerificado: true` deve marcar `Usuario.EmailVerificadoEm` e `ApplicationUser.EmailConfirmed`; com `false`, ambos ficam não verificados. Reutilizar a implementação transacional de `CriarComSenhaAsync` sem senha para não duplicar lógica.

- [ ] **Step 1: Adaptar os testes**

- Usuário existente confirma link e entra; usuário novo é criado (com `Usuario` **e** `ApplicationUser`); token reutilizado/expirado falha.
- Nenhum `Sessao` é criado dentro do serviço de token; a sessão é criada pelo endpoint via `AssinarAsync`.
- Atualizar/remover os testes de `ValidarSessaoAsync`/`SairAsync` em `ServicoDeAutenticacaoTests` e ajustar os que usam `SessaoCriada`.
- Adicionar teste de que `GarantirUsuarioAsync(..., emailVerificado: true/false)` cria os dois lados com o estado correto e é idempotente.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Application.Tests tests/Fisga.Api.Tests --filter "LinkMagico|Confirmar|GarantirUsuario"`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Ajustar `CadastroDeUsuario`/`ICadastroDeUsuario`, `ServicoDeAutenticacao` e `Contratos`. O serviço valida o token, garante o usuário via `CadastroDeUsuario`, atualiza verificação/acesso e devolve `ResultadoDaConfirmacao` — sem tocar em `Sessao` ou cookie.

- [ ] **Step 4: Implementar o endpoint e remover o legado**

`/api/auth/confirmar` chama `IdentitySessionService.AssinarAsync` e redireciona. Remover `SessaoMiddleware` do `Program.cs`, deletar o arquivo, remover `DefinirCookie`, manter `LimparCookie`.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS, sem middleware de sessão legado no pipeline.

- [ ] **Step 6: Commit**

```bash
git add src/Fisga.Application src/Fisga.Infrastructure src/Fisga.Api tests
git commit -m "feat(auth): sign in magic links through identity"
```

---

### Task 7: Consentimento formal

**Files:**
- Create: `src/Fisga.Application/Identidade/ConsentimentoService.cs`
- Create: `src/Fisga.Api/Middleware/ConsentimentoMiddleware.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Program.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Modify: `src/Fisga.Api/appsettings.json`
- Create: `tests/Fisga.Api.Tests/ConsentimentoTests.cs`

**Interfaces:**
- Produces: `LegalOptions { TermsVersion, PrivacyVersion }` ligado a `Legal:TermsVersion`/`Legal:PrivacyVersion` (valor inicial `2026-11-17`).
- Produces: `ConsentimentoService.Esta PendenteAsync(Guid usuarioId, CancellationToken)` e `RegistrarAsync(Guid usuarioId, string termosVersao, string privacidadeVersao, CancellationToken)`.
- Produces: `POST /api/conta/consentimento` com `{ termosVersao, privacidadeVersao }`, validando se as versões enviadas são as vigentes.
- Modifies: `MeResponse` ganha `bool ConsentimentoPendente`.
- Consumes: `IConsentimentoRepository`/`Consentimento` da Task 1 e `HttpContext.UsuarioId()` da Task 5.

**Decisões de implementação (vinculantes):**
- A verificação é feita no banco a cada requisição autenticada, via `IConsentimentoRepository.TemVersaoAsync` — **não** usar claims de versão no cookie. Isso evita cookie obsoleto quando a versão muda e dispensa reemissão de cookie ao aceitar. Não alterar `IdentitySessionService` nesta tarefa.
- O middleware só atua quando há usuário autenticado resolvido em `HttpContext.Items` (ou seja, depois de `SessaoIdentityMiddleware`). Usuário não autenticado continua sendo tratado pelos endpoints como hoje.
- Allowlist exata, quando o aceite está pendente: `GET /api/auth/me`, `POST /api/auth/sair` e `POST /api/conta/consentimento`. Todo outro `/api` responde 403 com problem details `consentimento_pendente`.
- O middleware grava o resultado em `HttpContext.Items["fisga.consentimentoPendente"]` para `/api/auth/me` devolver o sinalizador ao frontend.
- Registrar aceite com versão diferente da vigente retorna 400; não gravar versões arbitrárias.
- Usuários antigos começam sem aceite da versão vigente; não criar aceite retroativo.

- [ ] **Step 1: Escrever testes**

Cobrir: login sem aceite da versão atual recebe 403 em `/api/diario` (ou outra rota de dados); `/api/auth/me` continua 200 com `consentimentoPendente=true`; `POST /api/conta/consentimento` com as versões vigentes grava os dois tipos e libera; versão errada retorna 400 e não grava; mudar `LegalOptions.TermsVersion` no host de teste (com restauração no `finally`) volta a bloquear; usuário não autenticado não é afetado pelo middleware.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test --filter Consentimento`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Registrar `LegalOptions` em `DependencyInjection`/`Program` com bind de `Legal`. Criar `ConsentimentoService` usando `IConsentimentoRepository`, `IRelogio`, `IUnitOfWork` e `LegalOptions`. No middleware, quando autenticado, consultar `EstaPendenteAsync`; se pendente e fora da allowlist, escrever problem details 403 com código `consentimento_pendente`. Inserir o middleware depois de `SessaoIdentityMiddleware` no `Program.cs`.

- [ ] **Step 4: Implementar o endpoint de aceite**

`POST /api/conta/consentimento` valida as versões enviadas contra `LegalOptions`, chama `RegistrarAsync` e devolve 204. Ajustar `MeResponse` e o endpoint `/api/auth/me` para incluir `ConsentimentoPendente`.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS, sem regressão nos fluxos de login/link mágico.

- [ ] **Step 6: Commit**

```bash
git add src/Fisga.Application src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): require formal terms and privacy consent"
```

---

### Task 8: Login com Google

**Files:**
- Create: `src/Fisga.Infrastructure/Identity/GoogleIdentityService.cs`
- Create: `src/Fisga.Infrastructure/Identity/GoogleIdentityValidator.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/appsettings.json`
- Create: `tests/Fisga.Api.Tests/GoogleLoginTests.cs`

**Interfaces:**
- Produces: `GoogleIdentityValidator.ValidarAsync(ExternalLoginInfo, CancellationToken) : Task<GoogleUsuario>`, com `GoogleUsuario(string Subject, Email Email, string? Nome)`.
- Produces: `GoogleIdentityService.EntrarOuCriarAsync(GoogleUsuario, CancellationToken) : Task<Usuario>` em Infrastructure.
- Produces: `GET /api/auth/google/disponivel`, `GET /api/auth/google/iniciar`, `GET /api/auth/google/concluir`.
- Consumes: `ICadastroDeUsuario`, `UserManager<ApplicationUser>` e `IdentitySessionService`.

**Decisões de implementação (vinculantes):**
- **Não adicionar `Google.Apis.Auth`.** O handler `AddGoogle` já valida o token com `ClientId`/`ClientSecret` e produz um `ExternalLoginInfo` confiável. O validator apenas verifica `Provider == "Google"`, `ProviderKey` (subject) não vazio, e-mail presente e `EmailVerified == true`.
- Sem `Google:ClientId`/`Google:ClientSecret`, o handler não é registrado (Task 2); `/api/auth/google/disponivel` retorna `{ disponivel: false }` e `/iniciar` retorna 503 com código `google_indisponivel`. O app continua bootando.
- Ordem de resolução: (1) `UserManager.FindByLoginAsync("Google", subject)` → entra na mesma conta; (2) e-mail já existe: **só vincula** se `Usuario.EmailVerificadoEm != null` **e** `ApplicationUser.EmailConfirmed == true`; (3) e-mail existente mas não verificado em qualquer dos lados → recusa com `google_email_nao_verificado_localmente`, sem criar conta e sem alterar o estado local; (4) e-mail novo → `GarantirUsuarioAsync(email, nome, emailVerificado: true)` e `AddLoginAsync`.
- **Nunca** chamar `GarantirUsuarioAsync(..., emailVerificado: true)` antes da checagem (2)/(3): desde a Task 6 isso promoveria `EmailConfirmed` de uma conta de senha não confirmada e permitiria tomada de conta.
- `AddLoginAsync` falhando (subject já vinculado a outro usuário) → recusa, nunca troca a conta.
- O callback `/signin-google` é interceptado pelo handler; `/api/auth/google/concluir` lê `AuthenticateAsync(IdentityConstants.ExternalScheme)`, monta o `ExternalLoginInfo`, chama o serviço e assina com `IdentitySessionService.AssinarAsync`.
- Redirecionamentos de sucesso seguem o link mágico; falhas vão para `/entrar/confirmar?estado=<codigo>`.
- Testes sem credenciais reais constroem `ExternalLoginInfo` diretamente e exercitam o serviço; o fluxo OAuth real fica para o E2E com credenciais na Task 13.

- [ ] **Step 1: Escrever testes de linking**

Quatro caminhos: vínculo Google existente entra na mesma conta; e-mail verificado existente é vinculado; e-mail local não verificado é recusado (e `EmailConfirmed` continua `false`); e-mail novo cria `Usuario` + `ApplicationUser`. Verificar que o `subject` fica em `AspNetUserLogins` e que um `subject` já vinculado a outro usuário é recusado. Testar também `ValidarAsync` com `EmailVerified=false` e `/api/auth/google/disponivel` com/sem configuração.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test --filter GoogleLogin`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Validator + serviço + endpoints conforme as decisões. Registrar o serviço em DI. Sem novos pacotes NuGet.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Infrastructure/Identity src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): add Google sign-in and account linking"
```

---

### Task 9: Conectar Google em `/conta`

**Files:**
- Create: `src/Fisga.Infrastructure/Identity/ConexaoGoogleService.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Modify: `src/Fisga.Api/Program.cs` (registro do serviço)
- Create: `tests/Fisga.Api.Tests/ConectarGoogleTests.cs`

**Interfaces:**
- Produces: `ConexaoGoogleService.ConectarAsync(ApplicationUser conta, GoogleUsuario google, CancellationToken) : Task`; `CriarIntencaoAsync(Guid usuarioId)` / `ValidarIntencaoAsync(string valor, Guid usuarioId)` com `IDataProtectionProvider`.
- Produces: `GET /api/auth/google/conectar`, `GET /api/auth/google/conectar/concluir`; `MeResponse.GoogleConectado`.
- Consumes: `GoogleIdentityValidator`/`GoogleIdentityService` da Task 8 e `UserManager<ApplicationUser>`.

**Decisões de implementação (vinculantes):**
- O cookie de intenção `fisga_google_intencao` é protegido por `IDataProtector` com purpose `google-connect`, `HttpOnly`, `Secure` conforme `Auth:CookieSecure`, `SameSite=Lax`, validade máxima de 5 minutos, e é apagado no callback (sucesso ou falha). O valor carrega `usuarioId`, `nonce` e `expiraEm`.
- O callback exige usuário autenticado com o **mesmo** `usuarioId` da intenção; intenção expirada, inválida ou de outro usuário é recusada.
- Exigir `GoogleUsuario.EmailVerified` e e-mail Google **igual** ao e-mail verificado da conta (`conta.Email`/`EmailConfirmed`), comparando normalizado. E-mail diferente ou conta local não verificada → recusa, sem gravar nada.
- Se o `subject` já estiver vinculado a outro usuário (`FindByLoginAsync`), recusar sem trocar de conta.
- Se já estiver vinculado ao usuário atual, confirmar sem erro e sem duplicar linha.
- Chamar **apenas** `AddLoginAsync`. Nunca `SignInAsync`, nunca criar nova `Sessao` ou novo cookie `fisga_auth`.
- O gate de consentimento da Task 7 continua válido: `/conectar` e o callback ficam bloqueados enquanto o aceite estiver pendente.
- Sem credenciais Google configuradas, `/conectar` retorna 503 `google_indisponivel`, como no login.

- [ ] **Step 1: Escrever testes**

Testar: mesmo e-mail → vincula e mantém o mesmo usuário; e-mail diferente → recusa; `subject` já vinculado a outro usuário → recusa; já vinculado ao atual → confirma; intenção expirada/inválida/de outro usuário → recusa; nenhum `Set-Cookie` de `fisga_auth` novo e nenhuma linha nova em `Sessoes`; `MeResponse.GoogleConectado` reflete `AspNetUserLogins`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test --filter ConectarGoogle`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Serviço, intenção protegida, endpoints e status no `/me`. Registrar no DI. Não alterar o fluxo de login da Task 8.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Infrastructure/Identity src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): allow explicit Google account linking"
```

---

### Task 10: Protótipo visual no OpenDesign

**Files:**
- Nenhum arquivo de produto; usar o projeto `fisga-ux` no OpenDesign.
- Atualizar `Projetos/fisga/docs/ux-audit.md` com o resultado.

**Interfaces:**
- Produces: protótipo aprovado de `/entrar`, `/criar-conta`, `/esqueci-senha`, `/redefinir-senha`, `/aceite` e `/conta` (senha + Google + sessões).

- [ ] **Step 1: Briefar e gerar no OpenDesign**

Pedir as telas com Google, e-mail/senha, link mágico, consentimento e estados de erro/sucesso. Validar 360, 390, 768, 1440 e 1920 px.

- [ ] **Step 2: Revisar e aprovar**

Conferir: uma ação principal por tela, textos em pt-BR, estados vazio/erro/carregando, acessibilidade e consistência com o design atual. A aprovação do usuário é obrigatória antes da UI.

- [ ] **Step 3: Commit da documentação**

```bash
git add Projetos/fisga/docs/ux-audit.md
git commit -m "docs(fisga): document identity auth screens"
```

---

### Task 11: Telas públicas de autenticação

**Files:**
- Modify: `web/src/pages/entrar.astro`
- Create: `web/src/pages/criar-conta.astro`
- Create: `web/src/pages/esqueci-senha.astro`
- Create: `web/src/pages/redefinir-senha.astro`
- Create: `web/src/pages/confirmar-email.astro`
- Create: `web/src/pages/aceite.astro`
- Modify: `web/src/pages/entrar/confirmar.astro`
- Create: `web/src/lib/auth.ts`
- Modify: `web/src/pages/hoje.astro`, `web/src/pages/diario.astro`, `web/src/pages/mapa.astro`, `web/src/pages/registrar.astro`, `web/src/pages/conta.astro`, `web/src/pages/boas-vindas.astro`, `web/src/pages/meu-diario.astro`
- Modify: `web/package.json`
- Create: `web/playwright.config.ts`
- Create: `web/tests/auth.spec.ts`

**Interfaces:**
- Consumes: `/api/auth/registrar`, `/entrar`, `/solicitar`, `/confirmar-email`, `/esqueci-senha`, `/redefinir-senha`, `/confirmar`, `/auth/me`, `/conta/consentimento`, `/auth/google/disponivel`.
- Produces: `web/src/lib/auth.ts` com `obterSessao()` (parse de `MeResponse`, incluindo `ConsentimentoPendente` e `GoogleConectado`) e `exigirConsentimento()` (redireciona para `/aceite?destino=…`).

**Decisões de implementação (vinculantes):**
- Usar o protótipo aprovado da Task 10 (projeto OpenDesign `fisga-auth-screens`) como referência de tokens, copy, estados e responsividade. Não inventar estética paralela.
- `MeResponse` agora tem `ConsentimentoPendente`; toda página autenticada listada acima deve redirecionar para `/aceite` quando o campo for `true`, antes de tentar carregar dados (evita 403 da Task 7).
- O botão do Google só aparece quando `GET /api/auth/google/disponivel` retornar `disponivel: true`.
- `/aceite` envia `{ termosVersao, privacidadeVersao }` com as versões vigentes exibidas na página; sem os dois checkboxes, o botão fica desabilitado com mensagem clara.
- Autenticação entre origens distintas: `fetch(..., { credentials: 'include' })`; cookie SameSite=Lax funciona porque web e API são subdomínios do mesmo site registrável.
- Testes: adicionar `@playwright/test` e config. O ambiente pode não ter Chromium instalado; se `npx playwright install chromium` falhar, registrar a limitação e cobrir o essencial com `astro check`, `npm run build` e testes HTTP de API — **não** inventar que E2E rodou.

- [ ] **Step 1: Escrever o teste Playwright (ou o fallback documentado)**

Cobrir: Google visível quando disponível e oculto quando não; cadastro por senha; login por senha; esqueci a senha; confirmação; link mágico; aceite obrigatório; erros genéricos. Rodar contra o dev server e ver falhar.

- [ ] **Step 2: Implementar as telas**

Seguir o protótipo. Formulários com labels, autocomplete correto, estados de loading/erro/sucesso e links de Termos/Privacidade. Sem lorem ipsum.

- [ ] **Step 3: Rodar o teste e ver passar**

Run: `cd web && npx playwright test tests/auth.spec.ts`
Expected: PASS. Se o navegador não estiver disponível: rodar `npm run check && npm run build` e registrar explicitamente o E2E não executado.

- [ ] **Step 4: Commit**

```bash
git add web/src web/package.json web/playwright.config.ts web/tests/auth.spec.ts
git commit -m "feat(web): add identity auth screens"
```

---

### Task 12: Conta com senha, Google e dispositivos

**Files:**
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Modify: `web/src/pages/conta.astro`
- Create: `web/tests/conta-auth.spec.ts`
- Create: `tests/Fisga.Api.Tests/ContaSenhaTests.cs`

**Interfaces:**
- Produces: `POST /api/conta/senha` com `{ senhaAtual?, novaSenha }`.
- Produces: `GET /api/conta/sessoes` continua com `atual: true`; `MeResponse.GoogleConectado` já existe.
- Consumes: `UserManager<ApplicationUser>`, `ISessaoRepository`, `IdentitySessionService`, Google da Task 9.

**Decisões de implementação (vinculantes):**
- Se a conta **não tem senha** (`HasPasswordAsync == false`): `AddPasswordAsync(conta, novaSenha)`, sem exigir `senhaAtual`.
- Se já tem senha: exigir `senhaAtual` e usar `ChangePasswordAsync`; erro genérico `senha_atual_incorreta` sem revelar detalhes.
- Validar `novaSenha` entre 10 e 128 caracteres **antes** de chamar a Identity (a Identity não tem máximo nativo). Sem exigir composição.
- Após adicionar/alterar senha, revogar todas as **outras** sessões do usuário (`RevogarTodasAsync(..., exceto: sessaoIdAtual)`) e manter a sessão atual. A troca de senha é evento de segurança.
- Não criar endpoint de desconectar Google nesta entrega; a UI mostra status e o botão “Conectar Google”.
- `/conta` separa em linhas: E-mail/senha; Google; Sessões ativas; Zona sensível. Uma ação primária por bloco.
- “Sair desta sessão” e “Sair de todos” usam os endpoints atuais; “Excluir conta” mantém confirmação explícita.
- A UI segue o protótipo aprovado e usa `credentials: 'include'`; feedback com `aria-live`, sem depender só de cor.

- [ ] **Step 1: Escrever os testes de API e Playwright**

API: adicionar senha a conta sem senha; alterar senha com senha atual correta; recusar senha atual errada; recusar nova senha <10 ou >128; sessões diferentes da atual são revogadas após troca.
Playwright: usuário sem senha cria senha; altera senha; vê status do Google; conecta Google com e-mail igual; erro com e-mail diferente; lista dispositivos; “sair desta sessão” e “sair de todos”.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test --filter ContaSenha` e `cd web && npx playwright test tests/conta-auth.spec.ts`
Expected: FAIL.

- [ ] **Step 3: Implementar API**

Adicionar o endpoint e contratos. Usar `UserManager`, validar tamanho, revogar outras sessões. Não alterar os fluxos de login.

- [ ] **Step 4: Implementar UI**

Atualizar `/conta` conforme o protótipo, com estados de carregando/erro/sucesso e feedback acessível.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test` e `cd web && npm run check && npx playwright test tests/conta-auth.spec.ts`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Fisga.Api web/src/pages/conta.astro web/tests/conta-auth.spec.ts tests/Fisga.Api.Tests
git commit -m "feat(account): manage password, Google and devices"
```

---

### Task 13: Migração, E2E e rollout

**Files:**
- Create: `Projetos/fisga/docs/superpowers/runbooks/2026-11-17-identity-rollout.md`
- Modify: `README.md`
- Modify: `docs/ux-audit.md`
- Create: `web/tests/identity-e2e.spec.ts`

**Interfaces:**
- Consumes: todas as tasks anteriores.
- Produces: runbook de migração/rollback e evidência de teste.

- [ ] **Step 1: Rodar a verificação completa**

```bash
cd Projetos/fisga
dotnet test
cd web && npm run check && npm run build
cd web && npx playwright test
```

Expected: tudo PASS.

- [ ] **Step 2: Testar a migration em cópia do Postgres**

Criar uma cópia do banco de produção, rodar `dotnet ef database update`, conferir contagens de `usuarios`/`pescarias` e que `AspNetUsers` tem o mesmo número de linhas. Registrar o resultado no runbook.

- [ ] **Step 3: Escrever o runbook**

Incluir: backup, janela, variáveis Google, volume persistente para `DataProtection:KeysPath` (`data/keys`), ordem do deploy, verificação de `/health` e `/ready`, login de teste, rollback por imagem anterior, comunicação de re-login e limpeza de artefatos.

- [ ] **Step 4: E2E final**

Fluxos: usuário antigo via link mágico → consentimento → diário; novo usuário por senha → confirmação → consentimento; Google novo; Google existente; conectar Google; reset; sessões. Verificar Sentry sem PII.

- [ ] **Step 5: Commit final**

```bash
git add Projetos/fisga/README.md Projetos/fisga/docs/ux-audit.md web/tests/identity-e2e.spec.ts
git commit -m "test(auth): verify identity migration end to end"
```

---

## Execution Handoff

- Antes de criar o worktree, stashar/descartar o experimento não commitado de Google direto que está
  na `main`; ele foi substituído pelo `AddGoogle` da Identity.
- Worktree: `feat/identity-google` via `superpowers:using-git-worktrees`.
- Execução recomendada: `superpowers:subagent-driven-development` (tarefas independentes com revisão entre etapas).
- O design doc e este plano devem ser lidos pelo implementador antes da Task 1.
- Nenhuma tarefa começa sem o teste correspondente falhando.
