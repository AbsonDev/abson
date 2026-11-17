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
- Create: `src/Fisga.Domain/Identidade/Consentimento.cs`
- Create: `src/Fisga.Infrastructure/Persistence/Configurations/ConsentimentoConfiguration.cs`
- Modify: `src/Fisga.Infrastructure/Persistence/FisgaDbContext.cs`
- Modify: `src/Fisga.Infrastructure/Fisga.Infrastructure.csproj`
- Modify: `Fisga.slnx`
- Create: migration em `src/Fisga.Infrastructure/Persistence/Migrations/`

**Interfaces:**
- Produces: `ApplicationUser : IdentityUser<Guid>`, `Consentimento`, `IConsentimentoRepository` (em `Fisga.Application.Identidade`), `FisgaDbContext` herdando `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.

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

`ApplicationUser` vazio, herdando `IdentityUser<Guid>`. `Consentimento` com `Id`, `UsuarioId`, `Tipo` (`Termos`/`Privacidade`), `Versao`, `AceitoEm`; factory `Criar` valida campos e versão não vazia. `FisgaDbContext` herda `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`, adiciona `DbSet<Consentimento> Consentimentos` e mantém os DbSets atuais.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests/Fisga.Api.Tests.csproj --filter Banco_cria_tabelas`
Expected: PASS.

- [ ] **Step 5: Criar a migration aditiva**

Run: `dotnet ef migrations add IdentityConsentimento --project src/Fisga.Infrastructure --startup-project src/Fisga.Api --output-dir Persistence/Migrations`
Expected: migration cria `AspNetUsers`, tabelas de Identity e `consentimentos`; não dropa nem renomeia tabelas atuais.

- [ ] **Step 6: Commit**

```bash
git add tests/Fisga.Api.Tests src/Fisga.Infrastructure src/Fisga.Domain/Identidade/Consentimento.cs
git commit -m "feat(auth): add identity schema and consent entity"
```

---

### Task 2: Identity, cookie e opções de segurança

**Files:**
- Create: `src/Fisga.Infrastructure/Identity/IdentityExtensions.cs`
- Modify: `src/Fisga.Infrastructure/DependencyInjection.cs`
- Modify: `src/Fisga.Api/Program.cs`
- Modify: `src/Fisga.Api/appsettings.json`
- Create: `tests/Fisga.Api.Tests/IdentityOptionsTests.cs`

**Interfaces:**
- Produces: `IdentityExtensions.AdicionarIdentity(IServiceCollection, IConfiguration)`; cookie `fisga_auth`; opções de senha/lockout/email.
- Consumes: `FisgaDbContext` da Task 1.

- [ ] **Step 1: Escrever o teste de opções**

```csharp
[Fact]
public void Identity_usa_cookie_e_politicas_do_spec()
{
    using var factory = new TestWebApplicationFactory();
    var identity = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
    var cookie = factory.Services
        .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
        .Get(IdentityConstants.ApplicationScheme);

    Assert.Equal("fisga_auth", cookie.Cookie.Name);
    Assert.True(identity.SignIn.RequireConfirmedEmail);
    Assert.Equal(10, identity.Password.RequiredLength);
    Assert.False(identity.Password.RequireNonAlphanumeric);
    Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
    Assert.Equal(TimeSpan.FromMinutes(15), identity.Lockout.DefaultLockoutTimeSpan);
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter Identity_usa_cookie`
Expected: FAIL (cookie padrão/opções ausentes).

- [ ] **Step 3: Implementar**

Em `IdentityExtensions`, usar `AddIdentity<ApplicationUser, IdentityRole<Guid>>()`, `AddEntityFrameworkStores<FisgaDbContext>()`, `AddDefaultTokenProviders()`, configurar `IdentityOptions` e `ConfigureApplicationCookie` com `fisga_auth`. Adicionar `AddAuthentication().AddGoogle(...)` com `SignInScheme = IdentityConstants.ExternalScheme`, `CallbackPath = "/signin-google"` e `SaveTokens = true`; ler `Google:ClientId`/`Google:ClientSecret`. Configurar `DataProtection` com `PersistKeysToFileSystem` usando `DataProtection:KeysPath`. Registrar em `DependencyInjection`. Em `Program`, chamar `app.UseAuthentication()` antes do middleware de sessão.

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

Testes: criar conta por senha cria `Usuario` e `ApplicationUser` com o mesmo Id; e-mail duplicado não cria segundo usuário; senha curta é rejeitada; `Usuario.EmailVerificadoEm` começa nulo.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter CadastroDeUsuario`
Expected: FAIL.

- [ ] **Step 3: Implementar provisionamento transacional**

`CadastroDeUsuario` usa `UserManager<ApplicationUser>` + `FisgaDbContext` + `IUsuarioRepository` + `IUnitOfWork` + `IDomainEventDispatcher`. Abrir transação, criar `Usuario`, criar `ApplicationUser` com o mesmo Id/Email/UserName, `CreateAsync`, salvar, publicar eventos, commit. Em falha, rollback e remover o usuário de domínio do contexto.

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

`POST /api/auth/entrar` usa `SignInManager.PasswordSignInAsync`; em sucesso, carrega `Usuario`, cria sessão de dispositivo e assina com claims. `POST /api/auth/confirmar-email` usa `UserManager.ConfirmEmailAsync`, atualiza `Usuario.VerificarEmail` e salva. `RequireConfirmedEmail=true` já está na Task 2.

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
- Modify: `src/Fisga.Application/Identidade/Contratos.cs`
- Modify: `src/Fisga.Infrastructure/Persistence/Repositories/EfSessaoRepository.cs`
- Create: `src/Fisga.Api/Middleware/SessaoIdentityMiddleware.cs`
- Create: `src/Fisga.Api/IdentitySessionService.cs`
- Modify: `src/Fisga.Api/HttpContextExtensions.cs`
- Modify: `src/Fisga.Api/Program.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Create: `tests/Fisga.Api.Tests/SessaoIdentityTests.cs`

**Interfaces:**
- Produces: `IdentitySessionService.AssinarAsync(ApplicationUser, Usuario, HttpContext, CancellationToken) : Task`; `SessaoIdentityMiddleware`; claim `sessao_id`; `ISessaoRepository.ObterPorIdAsync(Guid, CancellationToken)`.
- Consumes: `SignInManager`/Identity da Task 2.

- [ ] **Step 1: Escrever testes**

Sessão ativa permite `/api/auth/me`; sessão revogada faz o próximo `/api/auth/me` retornar não autenticado; “sair” revoga só a atual; “sair de todos” revoga todas e invalida cookies.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter SessaoIdentity`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Criar helper `AssinarAsync(ApplicationUser, Usuario, HttpContext)` que cria `Sessao` (hash do `sessao_id` em `TokenHash`), chama `SignInManager.SignInWithClaimsAsync` com `sessao_id` e atualiza `Items`. Middleware lê o claim, busca a sessão, valida `EstaAtiva`; se inválida, `SignOutAsync` e limpa `Items`. `ExigirUsuarioId` usa `UsuarioAtual()`.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter SessaoIdentity`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Application/Identidade/Contratos.cs src/Fisga.Infrastructure/Persistence/Repositories/EfSessaoRepository.cs src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): bridge identity cookie to device sessions"
```

---

### Task 6: Link mágico usando a Identity

**Files:**
- Modify: `src/Fisga.Application/Identidade/ServicoDeAutenticacao.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Create: `tests/Fisga.Api.Tests/LinkMagicoIdentityTests.cs`
- Modify: `tests/Fisga.Application.Tests/ServicoDeAutenticacaoTests.cs`

**Interfaces:**
- Produces: `ServicoDeAutenticacao.ConfirmarAsync(...) : Task<ResultadoDeConfirmacao>` com `Usuario` e `PrimeiroAcesso`.
- Consumes: `CadastroDeUsuario` da Task 3 e `AssinarAsync` da Task 5.

- [ ] **Step 1: Adaptar os testes**

Usuário existente confirma link e entra; usuário novo é criado; token reutilizado/expirado falha; nenhum `Sessao` é criado dentro do serviço de token.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Application.Tests tests/Fisga.Api.Tests --filter "LinkMagico|Confirmar"`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Remover a criação de sessão/cookie do `ServicoDeAutenticacao`; ele valida o token, garante o usuário via `CadastroDeUsuario`, atualiza verificação/acesso e devolve o usuário. O endpoint `/api/auth/confirmar` chama `AssinarAsync` e redireciona.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Application.Tests tests/Fisga.Api.Tests --filter "LinkMagico|Confirmar"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Application/Identidade/ServicoDeAutenticacao.cs src/Fisga.Api/Endpoints/AuthEndpoints.cs tests
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
- Create: `tests/Fisga.Api.Tests/ConsentimentoTests.cs`

**Interfaces:**
- Produces: `LegalOptions { TermsVersion, PrivacyVersion }`, `IConsentimentoRepository`, `POST /api/conta/consentimento`, claims `consentimento_termos`/`consentimento_privacidade`.
- Consumes: `Consentimento` da Task 1.

- [ ] **Step 1: Escrever testes**

Login sem aceite da versão atual recebe 403 em `/api/diario`; `/api/auth/me` continua 200 com `consentimentoPendente=true`; aceitar grava tipo/versão/data e libera; mudar a versão em configuração volta a bloquear.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter Consentimento`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Adicionar `LegalOptions` em `appsettings` com versão `2026-11-17` para Termos e Privacidade. No sign-in, consultar consentimentos e adicionar claims de versão. `ConsentimentoMiddleware` compara claims com `LegalOptions` e retorna 403 `consentimento_pendente` para rotas de dados, exceto allowlist. `POST /api/conta/consentimento` grava e reemite o cookie com as claims atualizadas.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter Consentimento`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Application/Identidade src/Fisga.Api tests/Fisga.Api.Tests
git commit -m "feat(auth): require formal terms and privacy consent"
```

---

### Task 8: Login com Google

**Files:**
- Create: `src/Fisga.Infrastructure/Identity/GoogleIdentityService.cs`
- Create: `src/Fisga.Infrastructure/Identity/GoogleIdentityValidator.cs`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/appsettings.json`
- Modify: `src/Fisga.Infrastructure/Fisga.Infrastructure.csproj`
- Create: `tests/Fisga.Api.Tests/GoogleLoginTests.cs`
- Modify: `tests/Fisga.Application.Tests/Fakes.cs`

**Interfaces:**
- Produces: `GoogleIdentityValidator.ValidarAsync(ExternalLoginInfo, CancellationToken) : Task<GoogleUsuario>`; `GoogleIdentityService.EntrarOuCriarAsync(GoogleUsuario, CancellationToken) : Task<Usuario>` em Infrastructure.
- Consumes: `CadastroDeUsuario` e `ExternalLoginInfo`/`SignInManager`.

- [ ] **Step 1: Escrever testes de linking**

Testar quatro caminhos: vínculo Google existente entra na mesma conta; e-mail verificado existente é vinculado; e-mail local não verificado é recusado; e-mail novo cria usuário. Verificar que o `subject` do Google fica em `AspNetUserLogins`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter GoogleLogin`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Adicionar `Google.Apis.Auth` e validar o `id_token` de `ExternalLoginInfo.AuthenticationTokens` com audiência do ClientId. Exigir `EmailVerified`. Implementar linking por `AspNetUserLogins` e auto-link só com conta local verificada. Endpoint `/api/auth/google/iniciar` desafia; `/signin-google` é o callback do handler; `/api/auth/google/concluir` assina via Identity.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter GoogleLogin`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Application/Identidade src/Fisga.Infrastructure src/Fisga.Api tests
git commit -m "feat(auth): add Google sign-in and account linking"
```

---

### Task 9: Conectar Google em `/conta`

**Files:**
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/ContaEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Create: `tests/Fisga.Api.Tests/ConectarGoogleTests.cs`

**Interfaces:**
- Produces: `GET /api/auth/google/conectar`, `GET /api/auth/google/conectar/concluir`, `GoogleVinculadoResponse`.
- Consumes: `GoogleIdentityValidator` e `SignInManager` da Task 8.

- [ ] **Step 1: Escrever testes**

Usuário logado conecta Google com o mesmo e-mail e continua na mesma conta; e-mail diferente é recusado; Google já vinculado a outro usuário é recusado; sessão atual permanece; nenhum novo cookie de login é emitido.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Fisga.Api.Tests --filter ConectarGoogle`
Expected: FAIL.

- [ ] **Step 3: Implementar**

Criar cookie de intenção `HttpOnly` de uso único e curta duração. O callback exige o usuário autenticado, valida o e-mail Google igual ao e-mail da conta, verifica se o `LoginProvider/ProviderKey` já pertence a outro usuário e então chama `AddLoginAsync`. Não chamar `SignInAsync` de novo.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Fisga.Api.Tests --filter ConectarGoogle`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Fisga.Api tests/Fisga.Api.Tests
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
- Create: `web/src/lib/auth.ts`
- Create: `web/tests/auth.spec.ts`
- Create: `web/playwright.config.ts`
- Modify: `web/package.json` (devDependency `@playwright/test`)

**Interfaces:**
- Consumes: endpoints das Tasks 3, 4, 7, 8.
- Produces: fluxo completo de entrada/cadastro/reset/confirmação/consentimento.

- [ ] **Step 1: Escrever o teste Playwright**

Cobrir: Google visível quando disponível; cadastro por senha; login por senha; esqueci a senha; confirmação; link mágico; aceite obrigatório; erros genéricos. Rodar contra o dev server e ver falhar.

- [ ] **Step 2: Implementar as telas**

Usar o protótipo da Task 10. Formulários com labels, autocomplete correto, estados de loading/erro/sucesso e links de Termos/Privacidade. O botão Google só aparece quando `/api/auth/google/disponivel` retornar true.

- [ ] **Step 3: Rodar o teste e ver passar**

Run: `cd web && npx playwright test tests/auth.spec.ts`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add web/src/pages web/src/lib/auth.ts web/tests/auth.spec.ts
git commit -m "feat(web): add identity auth screens"
```

---

### Task 12: Conta com senha, Google e dispositivos

**Files:**
- Modify: `web/src/pages/conta.astro`
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/ContaEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/AuthContracts.cs`
- Create: `web/tests/conta-auth.spec.ts`

**Interfaces:**
- Produces: `POST /api/conta/senha`, `GET /api/conta/sessoes` com `sessao_id`, status do Google.
- Consumes: sessão da Task 5 e Google da Task 9.

- [ ] **Step 1: Escrever o teste Playwright**

Usuário existente sem senha cria senha; altera senha; vê status do Google; conecta Google com e-mail igual; vê erro com e-mail diferente; lista dispositivos; sai desta sessão e de todos.

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd web && npx playwright test tests/conta-auth.spec.ts`
Expected: FAIL.

- [ ] **Step 3: Implementar API e UI**

Adicionar endpoints de senha (`AddPasswordAsync`/`ChangePasswordAsync`) e status de Google. Na UI, separar “Senha”, “Google” e “Sessões”, com feedback e confirmação antes de ações destrutivas.

- [ ] **Step 4: Rodar e ver passar**

Run: `cd web && npx playwright test tests/conta-auth.spec.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/pages/conta.astro web/tests/conta-auth.spec.ts src/Fisga.Api
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

Incluir: backup, janela, variáveis Google, ordem do deploy, verificação de `/health` e `/ready`, login de teste, rollback por imagem anterior, comunicação de re-login e limpeza de artefatos.

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
