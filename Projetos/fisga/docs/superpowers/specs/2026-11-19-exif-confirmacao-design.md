# Fisga — confirmação explícita dos dados da foto

**Data:** 19/11/2026  
**Status:** aprovado pelo usuário em 19/11/2026  
**Escopo:** `/registrar` (web) + protótipo no OpenDesign; sem mudança de API.

## Intenção

Uma foto nunca deve alterar o registro sem decisão do pescador. Hoje `aplicarExif()`
(`web/src/pages/registrar.astro:584-632`) lê `DateTimeOriginal` e GPS no primeiro upload,
grava data e período direto nos campos (`:596-597`) e só depois mostra um aviso dizendo
"Confirme antes de salvar" — cuja única ação é "Ocultar", que não desfaz nada. A confirmação
é fictícia. O geocódico inverso ainda escreve o nome da cidade em `local` (`:616`), que
alimenta a projeção pública do link, do troféu e do PDF.

## Objetivos

- Nenhum campo ser preenchido por EXIF/GPS antes de "Aplicar".
- Data e período digitados manualmente nunca serem sobrescritos.
- "Ignorar" valer como decisão pelo resto da sessão, sem reaparecer a cada foto.
- O local exato nunca vir do GPS: a sugestão fica em cidade/UF.

## Não-objetivos

- Não pedir confirmação para cada foto enviada, nem para fotos sem dados úteis.
- Não mudar o contrato da API, o `/api/locais/reverso` ou a projeção `LocalPublico`.
- Não reabrir a decisão de níveis de privacidade já entregue na PR #48.

## Anti-objetivos

- Aviso informativo que pareça confirmação (o bug atual).
- Modal que interrompa a captura no celular.
- Campo `local` preenchido com coordenada ou ponto exato.

## Restrições

- Copy em pt-BR, direta e calma; padrões visuais de `design-system/fisga/DESIGN.md`.
- Referência visual: artefato do OpenDesign `fisga-produto/registrar/index.html`.
- Aviso inline, não modal; não bloqueia a digitação nem o upload.
- Alvos de toque de 44 px, foco visível, status anunciado por live region.
- Cobertura E2E existente em `web/` e comandos de validação do repositório.

## Abordagens consideradas

| Opção | Trade-off |
| --- | --- |
| A — aviso inline com "Aplicar"/"Ignorar" (escolhida) | Mantém o fluxo linear e funciona anônimo; exige guardar as sugestões em memória até a decisão. |
| B — `<dialog>` modal no upload | Sinal mais forte, mas interrompe no celular e foge de "uma decisão por tela" do DESIGN.md. |
| C — sugestões na etapa 4 (Revisão) | Não interrompe a captura, mas adiciona estado ao formulário e atrasa a correção de campo vazio. |

## Decisões

1. **Quando:** no upload da primeira foto com data ou GPS válidos, antes de qualquer escrita
   nos campos. Fotos sem dados úteis não abrem aviso.
2. **Onde:** aviso no topo do formulário, abaixo do banner anônimo; `role="status"` com
   ações "Aplicar nos campos vazios" e "Ignorar".
3. **Aplicar:** preenche somente `data`, `periodo`, `cidade` e `estado` quando estiverem
   vazios; `local` nunca é preenchido pelo GPS.
4. **Ignorar:** descarta as sugestões e marca a sessão como decidida; nenhum campo muda.
5. **Escopo da leitura:** continua valendo a primeira foto com dados úteis; o guard
   `exifAplicado` passa a representar "decisão tomada", não "dados aplicados".
6. **Edição/carga de rascunho:** não disparam aviso; mantêm `exifAplicado = true` como hoje.

## Deixado para o implementador

- Nome e formato do estado em memória, e o ponto exato de `await` no fluxo de upload.
- Texto final do aviso e dos estados pós-Aplicar/pós-Ignorar, conforme o artefato do OpenDesign.
- Estrutura dos testes E2E (mock de `exifr` e de `/api/locais/reverso`, dados de exemplo).
