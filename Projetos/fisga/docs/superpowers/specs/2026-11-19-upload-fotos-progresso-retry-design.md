# Especificação — Upload de fotos com progresso, retry e HEIC (19/11/2026)

## Intenção

A foto é o troféu do registro. Hoje anexá-la no mar é a etapa mais frágil do
`/registrar`: HEIC (padrão do iPhone) nem aparece no seletor, não há feedback
de envio e uma falha de rede aborta o lote inteiro. Queremos que a pessoa saiba
o que acontece com cada foto e possa repetir só o que falhou, sem perder o que
já subiu.

## Goals

1. HEIC/HEIF aparece no seletor; se o navegador decodificar nativamente, a
   foto entra no fluxo normal.
2. Se não decodificar, aviso inline claro com alternativa — sem modal e sem
   bloquear o formulário.
3. Cada foto mostra progresso individual durante o envio.
4. Falha de rede oferece "Tentar de novo" apenas para as fotos que não subiram;
   as concluídas não são duplicadas.
5. O Salvar continua protegido durante o envio, mas o estado do envio passa a
   ser visível e anunciado em vez de um botão sem explicação.

## Non-goals

- Fila persistente entre recarregamentos: o rascunho guarda URLs, não arquivos.
- Upload em segundo plano ao sair da tela.
- Conversão HEIC com biblioteca/WASM.
- Mudança no endpoint `POST /api/midia`, no contrato OpenAPI ou no banco.

## Anti-goals

- Barra de progresso que inventa percentual de bytes.
- Retry que reenvia o lote inteiro e duplica fotos já salvas.
- Aviso de erro só por cor, sem palavra e ícone.
- Modal ou scrim que interrompa a digitação.

## Estado real que condiciona

- `web/src/pages/registrar.astro:611` — input `accept="image/jpeg,image/png,image/webp"`,
  `multiple`.
- `:677-704` — `for (const arquivo of paraEnviar)` com `comprimirImagem()` →
  `FormData` → `POST ${API}/api/midia`. `uploadsPendentes` e
  `botaoSalvar.disabled` envolvem o lote; um único `catch` aborta tudo; no
  sucesso, `fotos.push(dadosMidia.url)` e `agendarRascunho()`.
- `:925-944` — `comprimirImagem()` usa `createImageBitmap(arquivo)` e
  `canvas.toBlob(..., 'image/jpeg', 0.82)`.
- O rascunho salva as URLs das fotos, não os arquivos.
- Artefato OpenDesign `fisga-produto/registrar/index.html` já tem `.photo-chip`
  (44 px, raio 999 px), `.note--alert` com "Tentar de novo", `.btn-quiet` e os
  tokens `--petrol`, `--clay`, `--line`.
- `web/tests/registrar-anonimo.spec.ts` já mocka `POST /api/midia`.

## Abordagens consideradas

| Abordagem | Trade-off | Decisão |
| --- | --- | --- |
| A — `heic2any` (decodificador WASM) | Aceita HEIC em qualquer navegador, mas adiciona dependência e baixa o decodificador sob demanda — justamente no cenário de sinal instável. | Não |
| B — recusar HEIC antes do seletor | Simples e previsível, mas o arquivo continua invisível e o motivo não aparece no momento da escolha. | Não |
| C — decode nativo com aviso de fallback | Aceita HEIC/HEIF no input; tenta `createImageBitmap`; se falhar, nota inline com alternativa. Sem dependência, sem payload extra, funciona offline. | **Escolhida** |
| D — retry do lote inteiro | Simples, mas reenvia e duplica fotos já concluídas. | Não |
| E — retry por foto com estado no chip | Mais estado no cliente, mas atende exatamente o critério de aceite e reusa componentes existentes do design system. | **Escolhida** |

## Decisões de implementação

- `accept` passa a incluir `image/heic,image/heif`.
- Cada arquivo selecionado vira uma entrada de upload com `id` estável,
  `estado` (`enviando` | `enviado` | `falhou` | `heic-recusado`) e `arquivo`.
  A entrada vive só na memória da página; a URL resultante continua indo para
  o rascunho.
- `comprimirImagem()` segue sendo a única via de conversão. Se
  `createImageBitmap` lançar para HEIC, a entrada vira `heic-recusado`, a nota
  inline aparece, nenhum campo é tocado e as outras fotos seguem.
- O envio passa a `try/catch` por entrada. Sucesso: `fotos.push(url)`, entrada
  vira `enviado`, `agendarRascunho()`. Falha: entrada vira `falhou`, mantém o
  `arquivo` em memória e oferece "Tentar de novo".
- "Tentar de novo" reenvia apenas a entrada falha, nunca as concluídas.
- Progresso real de bytes via `XMLHttpRequest.upload.onprogress`, com o mesmo
  payload `FormData` e `withCredentials = true`; tratamento de 401 mantém a
  chamada a `encerrarSessaoExpirada()`. A barra é decorativa e `aria-hidden`;
  o texto nomeia a etapa e o arquivo.
- `uploadsPendentes` conta entradas em `enviando`. O Salvar segue desabilitado
  enquanto houver envio, mas uma linha em `role="status"` diz
  "Enviando 2 de 3 fotos…".
- Copy pt-BR e estados visuais seguem o artefato do OpenDesign
  `fisga-produto/registrar/index.html` e o `DESIGN.md` do projeto — nenhuma
  cor ou componente novo. O artefato já contém `.photo-chip`, `.note--alert`
  com "Tentar de novo", `.btn-quiet` e os tokens necessários; os estados
  novos são composição desses componentes, não uma linguagem visual paralela.
- Sem alteração de contrato, API, banco ou configuração. Mudança é web-only e
  o rollback é um revert.

## Fora de escopo

- Fila persistente, upload em background, conversão WASM, mudança do
  `/api/midia`.

## Validação

- `npm run check`, `npm run build` e `npm run test:e2e` em `web/`.
- E2E mockando: HEIC decodificado, HEIC recusado com nota, progresso por foto,
  falha isolada e retry sem duplicar as concluídas.
- Markdownlint na raiz e indexação em `docs/INDEX.md`.

## Limitação da trilha OpenDesign

Tentei gerar uma revisão do artefato com o OpenDesign em 19/11/2026. Os dois
runtimes disponíveis falharam antes de produzir qualquer arquivo: o adaptador
chama `opencode run --dir`, sinal não suportado pelo OpenCode v2.3.2 instalado,
e o runtime BYOK exige configuração de provider que não está disponível. Não
troquei de modelo/provider por conta própria. Como fallback, usei o artefato
existente `fisga-produto/registrar/index.html` e seu `DESIGN.md`, lidos antes
desta spec, como referência visual — eles já trazem os componentes e tokens
necessários. Registrar aqui para que a limitação não seja confundida com
ausência da etapa de design.

## Referências

- Issue #52 — promovida de draft privado com acordo explícito em 19/11/2026.
- Artefato OpenDesign `fisga-produto/registrar/index.html` e seu `DESIGN.md`.
