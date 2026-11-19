# ADR 0005 — Conta obrigatória para o diário

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O diário anônimo por token era fácil de começar, mas o token era a única chave: limpar o navegador
ou trocar de aparelho significava perder a listagem. Também não havia como provar ownership,
bloquear abuso, oferecer conta ou preparar assinatura.

Como o produto ainda não tinha usuários reais, era o momento de remover o legado em vez de conviver
com dois modelos.

## Decisão

- `Pescaria.UsuarioId` é **obrigatório**; não existe mais pescaria sem dono.
- `DiarioTokenHash` e `EditTokenHash` saem do modelo.
- `POST/PUT /api/pescarias`, `GET /api/diario` e `POST /api/midia` exigem sessão.
- Edição é autorizada por `Pescaria.PertenceA(usuarioId)`; outro usuário recebe 404 para não
  revelar a existência do recurso.
- Links públicos de pescaria, troféu e PDF continuam acessíveis sem login.
- A migração limpa pescarias de teste antes de tornar `UsuarioId` obrigatório.

## Consequências

- O diário acompanha a conta em qualquer aparelho.
- Não há link de backup para perder nem token para vazar.
- O isolamento entre usuários passa a ser uma invariante testada.
- O cadastro continua necessário para salvar; desde o [ADR 0014](0014-registro-anonimo.md), o
  formulário fica ativo para anônimos e a conta só é pedida na confirmação do envio.
- A evolução para workspaces, membros ou compartilhamento privado pode ser feita sobre `UsuarioId`.
