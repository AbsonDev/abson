# ADR 0004 — Login por link mágico e sessão em cookie

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O Fisga nasceu com um token anônimo no navegador. Ele funcionava para validar, mas não dava
continuidade entre aparelhos, recuperação após limpar o navegador nem base para conta, assinatura ou
comunidade. A maioria dos pescadores não quer criar mais uma senha para um app de uso esporádico.

## Decisão

- O login é **passwordless**: o usuário informa o e-mail e recebe um link de uso único.
- O link vale 15 minutos, só o mais recente funciona e o banco guarda apenas SHA-256 do token.
- A confirmação cria um usuário se necessário, verifica o e-mail e cria uma **sessão de 30 dias**.
- A sessão viaja em cookie `HttpOnly`, `Secure` em produção e `SameSite=Lax`.
- O cookie é emitido em `api.fisga.abson.tech`; site e API são same-site, então CORS com
  `credentials` funciona sem BFF.
- Logout revoga a sessão no servidor. “Sair de todos” revoga todas as sessões do usuário.
- O primeiro acesso passa por `/boas-vindas` para nome e preferência de e-mail.

## Consequências

- Não há senha para vazar, esquecer ou redefinir.
- O e-mail passa a ser a chave de recuperação; se o provedor falhar, o login falha.
- O domínio do remetente precisa ser verificado no provedor (Resend) para entregar a qualquer
  destinatário.
- Sessões no servidor permitem revogação imediata e lista de dispositivos.
- O fluxo é same-site; se o site e a API forem separados em domínios diferentes no futuro, será
  necessário um BFF ou outro esquema de cookie.
