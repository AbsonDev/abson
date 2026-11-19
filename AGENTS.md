# AGENTS.md — monorepo abson

## O que é este repositório

Monorepo do portfólio. O site institucional e a API ficam em `apps/`; os
projetos de produto ficam em `Projetos/`. Deploy via Coolify.

## Onde está o quê

| Caminho | Projeto |
| --- | --- |
| `apps/` | abson.tech (Astro SSR + ASP.NET Core) |
| `Projetos/fisga/` | Fisga (app de pesca), em produção |
| `Projetos/precifica/` | Precifica |
| `Projetos/cardapio-no-lucro/` | Cardápio no Lucro |

## Antes de mexer

- Leia `Projetos/fisga/AGENTS.md` antes de alterar qualquer coisa do Fisga.
- Leia `Projetos/fisga/docs/INDEX.md` para a documentação de desenvolvimento.
- Nunca commite segredos (`.env`, tokens, DSNs, chaves). Configuração vive no Coolify.

## Regras gerais

- UI e copy em português do Brasil.
- Commits pequenos e convencionais.
- `Projetos/fisga/**` tem CI e CD próprios.
- Mudança de comportamento, contrato, configuração ou operação atualiza a documentação no mesmo PR.
