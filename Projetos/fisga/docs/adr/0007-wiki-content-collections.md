# ADR 0007 — Wiki estática com Content Collections e Pagefind

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O Fisga tinha apenas quatro páginas indexáveis e nenhum conteúdo para atrair buscas de cauda longa
(“tamanho mínimo do tucunaré”, “defeso da tainha”, “melhor isca para robalo”). O produto é uma
ferramenta; faltava o topo do funil. Também faltava conteúdo para cumprir a promessa de pesca
responsável e alimentar a newsletter.

## Decisão

- Criar a **Wiki do Pescador** em `/wiki`, com espécies, técnicas, iscas, regiões, defeso,
  iniciantes, segurança, glossário e fontes.
- Usar **Astro Content Collections** com frontmatter tipado por Zod e arquivos Markdown versionados.
- Renderizar as páginas da wiki como **estáticas** (`prerender = true`), mesmo com o restante do site
  em SSR.
- Gerar a busca com **Pagefind** no `postbuild`, restrita a `wiki/**/*.html`.
- Gerar uma lista de precache (`wiki-precache.json`) e cachear as páginas e o índice no service
  worker para leitura offline.
- Cada página tem data de revisão, fontes e aviso de que regulamentação não substitui a legislação.
- O CTA “Registrar captura” usa `/registrar?especie=...` para pré-preencher o formulário.

## Consequências

- Conteúdo rápido, barato, SEO-friendly e offline, sem CMS ou banco novo.
- O build precisa rodar `pagefind` e o script de precache; ambos estão no `postbuild`.
- A busca só existe após o build (em desenvolvimento, a página mostra fallback).
- Regulamentação exige revisão humana; não é automatizada.
- A wiki pode crescer para 100+ páginas sem mudar a arquitetura.
