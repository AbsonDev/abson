# ADR 0003 — Foto no servidor, card do troféu no navegador

**Status:** aceito · **Data:** 2026-11-17

## Contexto

Foto e troféu são o lado emocional do produto. Gerar PNG no servidor exigiria pipeline de imagem,
fontes, armazenamento e custo por render. O card também precisa acompanhar o layout da marca.

## Decisão

- A foto é comprimida/reencodada no navegador e enviada para `POST /api/midia`.
- O servidor aceita JPG/PNG/WebP até 5 MB, salva com nome aleatório e serve em `/media` com CORS.
- O **card do troféu** é desenhado no navegador via Canvas API (1080 × 1080) e baixado como PNG.
- O PDF é gerado no servidor com QuestPDF e não depende do card.

## Consequências

- Nenhum worker de imagem no MVP; custo de CPU fica no cliente.
- O reencode remove metadados EXIF e limita o tamanho.
- O canvas exige CORS nas fotos; o adaptador de mídia já responde com `Access-Control-Allow-Origin`.
- Para compartilhamento automático no futuro, será necessário render server-side ou uma função de
  imagem.
