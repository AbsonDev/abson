# Sentry: identidade opaca de usuário — design

Data: 2026-11-18. Mudança pequena; design aprovado em conversa.

## Intenção

Permitir que o Sentry conte `usersImpacted` sem enviar PII. Hoje `SentryScrubber.ScrubEvent`
faz `evento.User = null!`, e as duas issues do Fisga mostram `usersImpacted: 0`.

## Objetivos

- Preservar somente `SentryUser.Id` com o Guid do usuário autenticado (identificador opaco).
- Remover `Email`, `Username`, `IpAddress` e `Other` de eventos **e** transações.
- Manter requisições anônimas sem `User` no evento.

## Não-objetivos

- Não enviar e-mail, nome, IP, cookie nem dado de sessão.
- Não alterar `SendDefaultPii = false` nem os filtros existentes de query/URL/mensagem.
- Não tocar nos autofixes do Seer nas issues `FISGA-API-4` e `FISGA-API-5`.
- Não resolver, ignorar ou comentar issues no Sentry.

## Abordagens

- **A (escolhida):** helper puro `IdentidadeSentry.ParaUsuario(Guid)` +
  `SentrySdk.ConfigureScope` no `SessaoIdentityMiddleware` + scrubber preservando só `Id`.
  Simples, testável, sem configuração nova.
- **B:** ID com HMAC e salt de ambiente. Mais privacidade, mas exige secret novo e a rotação
  do salt quebra a correlação histórica de usuários.
- **C:** manter `usersImpacted: 0`. Não atende ao objetivo.

## Decisões de implementação

- Ponto de instrumentação: `SessaoIdentityMiddleware.InvokeAsync`, imediatamente após
  `context.DefinirSessao(sessao, usuario)`, apenas quando `usuario is not null`.
- Chamada: `SentrySdk.ConfigureScope(scope => scope.User = IdentidadeSentry.ParaUsuario(usuario.Id))`.
- `ScrubEvent` e `ScrubTransaction` trocam `User = null!` por `LimparUsuario(User)`.
- Convenção de teste: xUnit com fakes manuais; o projeto não tem biblioteca de mock.

## Riscos

- `ConfigureScope` depende do escopo por requisição do SDK ASP.NET Core. Se o escopo vazar
  entre requisições, o ID pode ser atribuído ao usuário errado. Mitigação: o helper só recebe
  um `Guid` e o scrubber limita o dado a esse campo; ainda assim, conferir emissões reais
  após o deploy.
- Nenhum PII é enviado por construção: o helper recebe apenas o `Guid`, nunca o objeto de usuário.
