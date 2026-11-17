# Fisga — Identity 2A com Google e e-mail/senha

**Status:** proposta para aprovação · **Data:** 2026-11-17
**Escopo:** autenticação. O diário, locais, pescarias e demais dados continuam no .NET/Postgres.

## 1. Intenção

Substituir o sistema de autenticação artesanal do Fisga por **ASP.NET Core Identity completo**,
mantendo o backend .NET, o PostgreSQL e o modelo atual de diário. Adicionar **Google** e
**e-mail/senha** sem perder o **link mágico** existente.

O usuário atual não tem senha. Ele precisa continuar entrando, agora com uma transição segura para
a Identity, sem perder o diário e sem depender de suporte manual.

## 2. Objetivos

- Autenticação oficial do ASP.NET Core com `UserManager` e `SignInManager`.
- Login com Google e login com e-mail/senha.
- Link mágico permanente como alternativa sem senha.
- Contas existentes migradas sem perder o vínculo com pescarias, locais e configurações.
- Manter controle de dispositivos: listar sessões ativas e “sair de todos”.
- Registrar formalmente o aceite de Termos/Privacidade, com versão e data.
- Permitir vincular explicitamente uma conta Google pela página `/conta`.
- Senhas sempre com hash da Identity; nenhum token ou senha em logs/Sentry.
- Rollout com backup, teste em cópia do banco e rollback claro.

## 3. Não objetivos

- Telefone/SMS nesta fase.
- Firebase Authentication, Firestore ou qualquer provedor externo de identidade.
- 2FA, passkeys e login social além do Google nesta entrega.
- Trocar o modelo de diário, locais, pescarias ou mídia.
- Redesenhar o produto inteiro; apenas as telas de autenticação e conta.

## 4. Anti-objetivos

- Deixar usuários existentes sem entrada.
- Ter dois sistemas de sessão concorrentes, cada um achando que manda na autenticação.
- Guardar senha em texto, logar ID token ou expor detalhes de erro que revelem se um e-mail existe.
- Perder a revogação de dispositivos.
- Fazer a migração sem backup ou sem possibilidade de rollback.

## 5. Restrições

- .NET 10 / ASP.NET Core 10.
- EF Core 10 + PostgreSQL em produção; SQLite no desenvolvimento.
- Astro 7 no frontend, com API em domínio separado (`api.fisga.abson.tech`).
- Deploy em Docker/Coolify.
- Sentry já configurado com scrubbing de PII.
- `Usuario` e `Sessao` atuais; link mágico via `TokenDeLogin`.
- Os usuários existentes já tiveram o e-mail verificado pelo link mágico.
- O fluxo de Google direto e a tabela `logins_externos` são um experimento não commitado e não
  implantado; serão descartados. Não há dados de produção a migrar dessa tabela.

## 6. Abordagens consideradas

1. **Google direto + link mágico.** Menor esforço, mas não atende e-mail/senha.
2. **Identity enxuta (2B).** Mantém `fisga_sessao` e usa apenas `PasswordHasher`; menor risco, mas não
   entrega o ciclo completo da Identity.
3. **Identity completa (2A).** Escolhida. Entrega `UserManager`, `SignInManager`, cookie, lockout,
   confirmação, reset e login externo.
4. **Firebase Auth.** Rejeitada: terceiro adicional, telefone pago, conflito com o modelo de usuário
   centrado em e-mail e sem ganho sobre a Identity para este stack.

## 7. Arquitetura escolhida

### 7.1 Usuário de autenticação

- `ApplicationUser : IdentityUser<Guid>` em `Fisga.Infrastructure/Identity`.
- `ApplicationUser.Id` é o **mesmo Guid** de `Usuario`.
- `ApplicationUser.Email` e `UserName` espelham `Usuario.Email`.
- `ApplicationUser` guarda apenas autenticação: hash, claims, logins externos, security stamp,
  lockout e tokens.
- `Usuario` continua no domínio, com nome, preferências, verificação e dados do produto.

### 7.2 DbContext

- `FisgaDbContext` passa a herdar
  `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.
- As tabelas da Identity ficam no schema `fisga`.
- As tabelas atuais (`usuarios`, `pescarias`, `locais_de_pesca`, etc.) não mudam de nome.
- Criação de usuário e `ApplicationUser` acontece na mesma transação.
- Exclusão de conta remove `ApplicationUser`, `Usuario` e dados dependentes.

### 7.3 Cookie e sessão

- O cookie da Identity é a única fonte de autenticação.
- Nome novo: `fisga_auth`; o antigo `fisga_sessao` é removido no login/logout.
- O cookie é `HttpOnly`, `Secure` em produção, `SameSite=Lax`, com validade de 30 dias.
- `Sessao` deixa de ser autenticação e vira **registro de dispositivo/revogação**:
  - no login, cria-se uma linha em `Sessao`;
  - o cookie recebe o claim `sessao_id`;
  - um middleware leve verifica se a sessão foi revogada;
  - se revogada, faz sign-out da Identity e responde 401;
  - “sair” revoga a sessão atual;
  - “sair de todos” revoga todas as sessões e atualiza o `SecurityStamp`.
- O middleware pode usar cache curto (30–60 s) para não consultar o banco em toda requisição.
- `ExigirUsuarioId()` passa a ler o claim `NameIdentifier`, que é o Id do `Usuario`.
- Escopo desta entrega: listar dispositivos, “sair desta sessão” e “sair de todos”. Revogação
  individual de outro dispositivo fica para uma fase posterior.

### 7.4 Google

- `AddGoogle()` oficial, com `ClientId`, `ClientSecret` e `CallbackPath = "/signin-google"`.
- **Login Google:** fluxo:
  1. `/api/auth/google/iniciar` desafia o handler.
  2. Google retorna para `/signin-google`.
  3. A API lê `ExternalLoginInfo`.
  4. Exige e-mail verificado.
  5. Procura `AspNetUserLogins` por `Google` + `subject`.
  6. Se não houver vínculo, procura `Usuario` com o mesmo e-mail **e e-mail já verificado no
     Fisga** (`Usuario.EmailVerificadoEm != null`). Só então vincula automaticamente.
  7. Se o e-mail existir mas a conta local não estiver verificada, **não vincula**: responde pedindo
     confirmação por link mágico. Isso evita pré-sequestro de conta.
  8. Se não existir conta, cria `Usuario` + `ApplicationUser` com e-mail verificado.
  9. Emite o cookie da Identity e a sessão de dispositivo.
- **Conectar Google em `/conta`:** fluxo separado:
  1. `/api/auth/google/conectar` exige sessão e cria um cookie de intenção `HttpOnly` de curta duração.
  2. Desafia o Google com `RedirectUri` para `/api/auth/google/conectar/concluir`.
  3. O callback exige que o usuário atual continue autenticado e que o e-mail verificado do Google
     seja **igual** ao e-mail verificado da conta Fisga.
  4. Se o Google já estiver vinculado a outra conta, recusa e não troca de conta.
  5. Se já estiver vinculado ao usuário atual, apenas confirma.
  6. Vincula ao usuário atual sem criar nova sessão nem trocar o cookie.
- Configuração no Google Cloud:
  - redirect `https://api.fisga.abson.tech/signin-google`;
  - redirect local `http://localhost:5184/signin-google`;
  - origens `https://fisga.abson.tech` e `http://localhost:4323`.

### 7.5 E-mail e senha

- `POST /api/auth/registrar`: nome, e-mail, senha.
- `POST /api/auth/entrar`: e-mail/senha.
- `POST /api/auth/esqueci-senha`: resposta genérica + e-mail de reset.
- `POST /api/auth/redefinir-senha`: token + nova senha.
- Confirmação de e-mail obrigatória para ativar o login por senha.
- `Usuario.EmailVerificadoEm` é a fonte de verdade do domínio; `ApplicationUser.EmailConfirmed`
  espelha esse estado.
- Cadastro por senha cria ambos como não verificados; a confirmação marca os dois.
- Link mágico e Google verificado marcam os dois.
- Reset com token de uso único e validade curta.
- Política:
  - mínimo 10 caracteres, máximo 128;
  - sem exigir maiúscula, número ou símbolo;
  - lockout após 5 tentativas por 15 minutos;
  - e-mail único e normalizado.
- O envio usa o `IEmailSender`/Resend atual.

### 7.6 Link mágico

- Permanece como método permanente.
- `TokenDeLogin` continua igual.
- Ao confirmar o link:
  - garante que existe `ApplicationUser` para o `Usuario`;
  - faz sign-in pela Identity;
  - cria a sessão de dispositivo;
  - redireciona para `/hoje` ou onboarding.
- Em `/conta`, o usuário pode “Criar senha” e “Conectar Google”; se já houver vínculo, mostra o
  status. Desconectar Google fica para uma fase posterior.

### 7.7 Consentimento

- O aceite é exigido para a versão vigente de Termos e Privacidade.
- Depois do login, se o usuário não tiver os aceites da versão atual, ele é redirecionado para
  `/aceite` antes de acessar o app.
- Enquanto houver aceite pendente, os endpoints de dados respondem 403; apenas `/api/auth/me`,
  `/api/auth/sair` e o endpoint de registro de consentimento continuam liberados.
- O registro guarda tipo, versão e data; não inventar aceite para usuários antigos.
- Quando a versão mudar, o interstício volta a aparecer para todos.

### 7.8 Sessões ativas e conta

- `/conta` continua listando dispositivos, com “sair desta sessão” e “sair de todos”, além do
  status/vínculo do Google.
- Revogação individual de outro dispositivo não entra nesta entrega.
- “Sair de todos” revoga as sessões e invalida cookies via `SecurityStamp`.
- Exclusão de conta remove Identity + domínio + arquivos, como hoje.

## 8. Modelo de dados

- Novas tabelas padrão da Identity no schema `fisga`.
- `usuarios` permanece.
- `sessoes` permanece com o schema atual; `TokenHash` passa a guardar o hash do `sessao_id` do
  dispositivo, não o token do cookie. A autenticação não depende mais desse hash.
- `tokens_de_login` permanece para o link mágico.
- `logins_externos` não é necessário e será descartado antes da implementação; a Identity usa
  `AspNetUserLogins`.
- Nova tabela `consentimentos`: `Id`, `UsuarioId`, `Tipo` (`Termos`/`Privacidade`), `Versao`,
  `AceitoEm`. Índice único por usuário + tipo + versão. O aceite é exigido para a versão vigente.
- A migration desta entrega é **aditiva**: não remove nem renomeia colunas/tabelas existentes.

## 9. Migração

1. Criar as tabelas da Identity.
2. Para cada `usuarios`:
   - criar `AspNetUsers` com o mesmo `Id`;
   - `Email` e `UserName` = e-mail atual;
   - `EmailConfirmed = true` quando `EmailVerificadoEm` existe;
   - sem hash de senha.
3. Invalidar as sessões atuais; todos entram uma vez.
4. Manter o link mágico funcionando para todos.
5. Não apagar nenhum dado de pescaria/local.
6. `logins_externos` não existe em produção; remover apenas do código/migration local, sem ação
   no banco de produção.
7. Manter o schema antigo compatível para rollback por imagem anterior.
8. Criar a tabela `consentimentos`; usuários existentes começam sem aceite da versão vigente e
   passam pelo interstício no próximo login. Não inventar aceite retroativo.

## 10. Telas

- `/entrar`: Google, e-mail/senha, “criar conta”, “esqueci a senha” e “entrar sem senha”.
- `/criar-conta`: nome, e-mail, senha e aviso de termos/privacidade. O registro formal de aceite
  fica pendente de decisão jurídica; o comportamento atual é consentimento implícito no aviso.
- `/esqueci-senha`.
- `/redefinir-senha`.
- `/confirmar-email`.
- `/aceite`: interstício de Termos/Privacidade para quem ainda não aceitou a versão vigente,
  inclusive usuários antigos e novos logins com Google/link mágico.
- `/conta`: senha, conectar Google, status do vínculo, sessões ativas e exclusão.
- O protótipo visual dessas telas passa pelo OpenDesign antes da implementação.

## 11. Segurança

- Rate limit em todos os endpoints de autenticação.
- Respostas genéricas em “esqueci a senha” e login inválido.
- Manter o `ProtecaoCsrfMiddleware` atual (allow-list de Origin) + `SameSite=Lax`; não introduzir
  o fluxo de tokens antiforgery do ASP.NET nesta entrega.
- `RequireConfirmedEmail = true` para login por senha; Google e link mágico são métodos de
  verificação e escrevem o mesmo estado.
- Auto-link Google só com e-mail Google verificado **e** conta local já verificada.
- O vínculo explícito exige e-mail Google verificado igual ao e-mail verificado da conta atual;
  nunca troca a conta autenticada.
- O cookie de intenção do vínculo é `HttpOnly`, curto e de uso único.
- O aceite formal é exigido antes de liberar o app; a versão vigente fica em configuração
  (`Legal:TermsVersion` e `Legal:PrivacyVersion`).
- Senhas e tokens nunca aparecem em logs, Sentry, métricas ou mensagens.
- Claims mínimas; `SecurityStamp` para invalidação global.
- Data Protection keys persistidas no servidor para sobreviver a restart/deploy.
- Revisão de dependências e configuração de cookie.

## 12. Observabilidade

- Métricas:
  - login Google iniciado/concluído/falhou;
  - login por senha concluído/falhou;
  - cadastro concluído;
  - reset solicitado/concluído;
  - migração de usuários.
- Sentry continua com scrubbing; nunca enviar e-mail, senha, token ou cookie.
- Alertas para falha de login/erro 5xx já existentes.

## 13. Rollout

1. Implementar em worktree `feat/identity-google`.
2. Rodar migration em cópia de produção.
3. Testar E2E local com Google de teste e e-mail/senha.
4. Backup do Postgres de produção.
5. Deploy em janela de baixo uso.
6. Rollback oficial: redeploy da imagem anterior sobre o schema aditivo, sem restore destrutivo.
   O backup do Postgres é a rede de segurança para catástrofe, não o primeiro caminho.
7. Comunicar que todos precisam entrar de novo uma vez.

## 14. Testes

- Unitários: linking Google, criação de usuário, hash, lockout, reset, link mágico, sessão.
- Integração: endpoints com `WebApplicationFactory`.
- Migration: contagem e integridade em cópia do banco.
- E2E: registro, login por senha, reset, Google, link mágico, sessão/dispositivos.
- Verificação de que Sentry/metricas não recebem segredos.

## 15. Critérios de aceite

- Usuário existente entra pelo link mágico e vê o mesmo diário.
- Usuário novo cria conta com e-mail/senha e confirma o e-mail.
- Usuário entra com Google e cai no mesmo diário se o e-mail verificado já existir.
- “Sessões ativas” lista dispositivos e “sair de todos” derruba todos.
- Todo usuário, inclusive antigo, registra o aceite da versão vigente antes de usar o app.
- Usuário autenticado consegue conectar o Google em `/conta` sem trocar de conta; e-mail diferente
  ou Google já vinculado a outro usuário é recusado com mensagem clara.
- Nenhum usuário fica bloqueado sem senha.
- Testes, build, migration e E2E passam.
- Produção saudável após o rollout.

## 16. Decisões deixadas para o plano

- Nomes exatos de classes/arquivos e ordem das migrations.
- Estratégia de cache da verificação de sessão.
- Textos finais dos e-mails de confirmação/reset.
- Layout exato das telas, após OpenDesign.
