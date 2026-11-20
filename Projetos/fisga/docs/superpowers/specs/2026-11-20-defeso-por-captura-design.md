# Fisga — aviso de defeso por captura no `/registrar`

**Data:** 20/11/2026
**Status:** aprovado pelo usuário em 20/11/2026 (opção A)
**Issue:** #57 (promovida do draft privado do quadro 6 com acordo explícito)
**Escopo:** `/registrar` (web); sem mudança de API, contrato ou banco.

## Intenção

Hoje o aviso de defeso é um único bloco na etapa 2 (`web/src/pages/registrar.astro:305`)
e `atualizarDefeso()` (`:975-996`) lê apenas `document.querySelector('.captura-especie')`,
a primeira captura. Quando a pessoa digita a espécie na etapa 3, o aviso é atualizado num
painel já oculto; na prática ele não aparece durante a decisão de manter o peixe, e da
segunda captura em diante nem existe. A meta é levar o aviso para a etapa 3, uma vez por
captura, com a espécie e a UF daquela linha.

## Objetivos

- Cada captura com espécie e UF preenchidas exibe seu próprio aviso na etapa 3.
- Trocar a espécie de uma linha atualiza só o aviso dela; as demais permanecem.
- Duas capturas de espécies diferentes exibem dois avisos independentes.
- O aviso some e é limpo quando a espécie ou a UF deixam de estar preenchidas.
- O link `Abrir órgão oficial` aponta para `/ferramentas/defeso?uf=<UF>`.

## Não-objetivos

- Não criar base de defeso por espécie/UF nem calcular status aberto/fechado. O aviso
  continua sendo um lembrete com link para o órgão oficial.
- Não alterar o contrato da API, o OpenAPI, migrações, configuração ou workflow.
- Não mexer em rascunho, EXIF, upload, foco entre etapas, privacidade do local ou pós-save.
- Não redesenhar a etapa 2 além de remover o bloco de defeso que migrou para a etapa 3.

## Anti-objetivos

- Manter um aviso global que só reflita a primeira captura.
- Duplicar o aviso na etapa 2 e na etapa 3 ao mesmo tempo.
- Anunciar a cada tecla em uma live region e poluir o leitor de tela.
- Usar `innerHTML` com a espécie digitada pelo usuário.

## Restrições

- Copy em pt-BR: `Antes de manter: confira tamanho mínimo e defeso de <espécie> em <UF>.`
  e link `Abrir órgão oficial`.
- Referência visual lida nesta tarefa: `DESIGN.md` e artefatos `registrar*` do projeto
  OpenDesign `fisga-produto`. O padrão de aviso é a nota inline descrita em
  "Components & interaction states" (alerta: clay misturado ao ink, nunca borda esquerda).
- Cobertura E2E em `Projetos/fisga/web/tests/`; comandos de validação do `AGENTS.md`.
- Documentação de desenvolvimento atualizada no mesmo PR e indexada em `docs/INDEX.md`.

## Abordagens consideradas

| Opção | Trade-off |
| --- | --- |
| A — nota inline por captura (escolhida) | Aviso junto da decisão, estado independente por linha, atende "para cada captura". Custo: uma linha a mais no card quando a espécie e a UF estão preenchidas. |
| B — bloco-resumo no topo da etapa 3 | Mais compacto, porém longe da linha e sem a mesma associação visual entre espécie e aviso. |
| C — mover o aviso único para a etapa 3 | Mudança mínima, mas continua só olhando a primeira captura e não atende o critério de duas capturas. |

## Decisões

1. Remover `<div id="defeso-alerta">` da etapa 2.
2. Em `criarLinhaCaptura()`, incluir
   `<div class="defeso-alerta oculto" role="note" data-defeso></div>` no fim da linha,
   antes de `.captura-fotos`, para ocupar a largura toda do card.
3. `atualizarDefeso()` passa a iterar `listaCapturas.querySelectorAll('.linha-captura')`
   e preencher o aviso de cada linha quando `espécie` (trim) e UF (2 letras) existirem.
   Usa `textContent` para a espécie e limpa com `replaceChildren()`.
4. Cada aviso recebe um `id` único (`defeso-<n>`), e o input `.captura-especie` recebe
   `aria-describedby` apontando para ele. O aviso mantém `role="note"`; não é live region.
5. `.defeso-alerta` ganha `grid-column: 1 / -1` e tem a definição duplicada removida.
   No mobile, o link do aviso tem alvo de 44 px (`inline-flex`, `min-height: 44px`).
6. A chamada existente de `atualizarDefeso()` em `formulario` (`input`/`change`) e após
   hidratar/editar continua valendo e passa a atualizar todas as linhas.

## Deixado para o implementador

- Nome do contador de ids (sugestão: `sequenciaDefeso`).
- Posição exata do contador junto às demais variáveis de estado do script.
- Estrutura dos testes E2E: nomes e asserções estão fixados no plano.
