# ADR 0013 — Identidade completa com ASP.NET Core Identity

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O login por link mágico funcionava, mas o produto precisava de senha como alternativa permanente,
login com Google, consentimento formal de Termos/Privacidade e uma lista de dispositivos com
revogação. O cookie próprio e a tabela `Sessoes` resolviam autenticação, porém não ofereciam
hash de senha, lockout, confirmação de e-mail, security stamp nem vínculo com provedor externo.

A decisão estava entre quatro caminhos: Google direto, Firebase Authentication, Identity enxuta só
para senha ou **ASP.NET Core Identity completa**. Os três primeiros exigiam manter dois sistemas de
identidade ou trocar a stack; o último preserva .NET + PostgreSQL e o diário existente.

## Decisão

- **ASP.NET Core Identity completo** com `ApplicationUser : IdentityUser<Guid>` compartilhando o
  mesmo `Guid` do agregado `Usuario`. As tabelas da Identity ficam no schema `fisga`.
- O cookie `fisga_auth` é a **única** autenticação. `Sessao` deixa de autenticar e passa a ser
  registro de dispositivo/revogação, ligado ao cookie pela claim `sessao_id`.
- `EmailVerificadoEm` no domínio é a fonte de verdade; `EmailConfirmed` espelha o estado.
- **Senha** de 10 a 128 caracteres, sem exigir composição; e-mail confirmado é obrigatório para
  entrar; lockout de 5 tentativas por 15 minutos.
- **Link mágico** continua permanente como alternativa sem senha, de uso único e 15 minutos.
- **Google é opcional**: sem credenciais o app boota, o botão fica oculto e a API responde
  `google_indisponivel`.
- **Auto-link do Google** só ocorre com e-mail Google verificado **e** conta local já verificada nos
  dois lados. Conta local não verificada é recusada sem criar ou alterar nada.
- **Conectar Google** em `/conta` é um fluxo separado, com cookie de intenção protegido, mesmo
  e-mail verificado e `AddLoginAsync` apenas; nunca troca a conta autenticada nem emite novo cookie.
- **Consentimento formal** em `consentimentos`, com tipo, versão e data. As versões vigentes vêm de
  `Legal:TermsVersion`/`Legal:PrivacyVersion`; o aceite é lido do banco a cada requisição
  autenticada, sem claims de versão no cookie.
- **Sessões** de 30 dias com “sair desta sessão” e “sair de todos”; não há revogação individual de
  outro dispositivo nesta entrega.
- Migration **aditiva**; rollback oficial pela imagem anterior + backup. O schema antigo permanece
  legível durante a janela de deploy.
- Chaves de Data Protection precisam de volume persistente (`data/keys`), senão todo deploy
  invalida cookies e links.

## Consequências

- O produto ganha senha, Google, confirmação, reset, lockout, consentimento e dispositivos sem
  trocar de stack nem perder o diário existente.
- Usuários antigos continuam entrando pelo link mágico; o primeiro login já cria a conta Identity
  com o mesmo Id.
- A tabela `Sessoes` deixa de ser credencial: o cookie da Identity é a autenticação e a linha é
  apenas dispositivo/revogação. Um `sessao_id` de outro usuário é recusado.
- O cookie legado `fisga_sessao` é apagado no login/logout e não autentica mais. Rollback exige
  revogar sessões abertas para forçar re-login na imagem anterior.
- Consentimento sem claims evita cookie obsoleto quando a versão muda; o custo é uma leitura por
  requisição autenticada, barata pelo índice de `consentimentos`.
- Google fica desligado até existirem `Google:ClientId`/`Google:ClientSecret`; o restante do login
  não depende dele.
- O runbook de rollout concentra backup, migration, volume de chaves, verificação e rollback.

## Referências

- Design: [`docs/superpowers/specs/2026-11-17-identity-google-design.md`](../superpowers/specs/2026-11-17-identity-google-design.md)
- Plano: [`docs/superpowers/plans/2026-11-17-identity-google.md`](../superpowers/plans/2026-11-17-identity-google.md)
- Runbook: [`docs/superpowers/runbooks/2026-11-17-identity-rollout.md`](../superpowers/runbooks/2026-11-17-identity-rollout.md)
- ADR anterior: [`0004-login-link-magico.md`](0004-login-link-magico.md)
