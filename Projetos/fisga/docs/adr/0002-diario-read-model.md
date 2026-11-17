# ADR 0002 — Diário como read model privado por token

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O diário precisa listar pescarias e calcular estatísticas sem exigir cadastro/login. Modelar um
agregado `Diario` com todas as pescarias dentro faria o agregado crescer indefinidamente e
dificultaria concorrência/edição.

## Decisão

- Cada `Pescaria` guarda o hash de um **diary token** gerado no navegador.
- O diário é um read model: `GET /api/diario` busca as pescarias pelo hash e calcula estatísticas na
  aplicação.
- O token cru fica no `localStorage`; o banco guarda apenas SHA-256.
- O link `/meu-diario?token=...` serve de backup entre dispositivos e é tratado como segredo.

## Consequências

- Sem login e sem tabela de usuário no MVP.
- Pescarias continuam agregados pequenos e independentes.
- Perder o token significa perder a listagem do diário (os links públicos das pescarias continuam).
- A evolução para conta real pode migrar o token para um `Usuario`/`Workspace`.
