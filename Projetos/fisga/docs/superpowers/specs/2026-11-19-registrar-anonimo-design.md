# Registrar anônimo — capturar primeiro, conta depois

**Data:** 2026-11-19
**Rota:** `/registrar`
** Issues:** [#43](https://github.com/AbsonDev/abson/issues/43), [#44](https://github.com/AbsonDev/abson/issues/44), [#45](https://github.com/AbsonDev/abson/issues/45)
**Status:** aguardando aprovação

## Contexto

Hoje, quem chega a `/registrar` sem sessão lê no aviso: “Você pode preencher o
formulário agora e entrar quando terminar”. O código, porém, chama
`bloquearFormulario()`: o `<form>` recebe `inert`, o leitor de tela e o teclado
não entram em nenhum campo e o único caminho é `/entrar`. A promessa e a
interface se contradizem na primeira experiência de quem não tem conta
(#43).

Mesmo depois de entrar, o rascunho local não cobre a fase anônima: os
listeners de `input`/`change` só chamam `agendarRascunho()` quando
`autenticado`; o debounce de 500 ms não tem flush em `pagehide`/
`beforeunload`; e `coletar()` descarta capturas que tenham só isca, “Solto” ou
foto (#44).

Por fim, quando existe rascunho, `restaurarRascunho()` preenche o formulário e
retorna antes de `aplicarCondicoesDaUrl()`. Quem escolheu condições em
`/condicoes` vê maré, lua, clima, data, período e local de uma pescaria antiga e
pode salvar o registro errado (#45).

O produto promete “capturar o dia de hoje” em contexto de sol, pressa e sinal
ruim. Pedir conta antes do primeiro campo inverte essa promessa.

## Referência visual

Artefato do OpenDesign (fora do repositório, `.superpowers/sdd/registrar-anonimo/design/`):
`registrar.html` + `DESIGN.md`. O projeto canônico é `fisga-produto`, com o
design system `user:fisga-2`. O artefato define os estados: anônimo padrão,
digitando/rascunho salvo, condições vindas do link, foto anônima, salvar sem
estar logado, depois do login, rascunho vazio, erro de leitura do rascunho e
salvo. O chrome de revisão (seletor de estados, artboards e anotações) **não**
entra no produto.

Tokens e regras que já existem e devem ser preservados: papel `#F8F4EA`,
superfície `#FFFDF8`, tinta `#172A2D`, petrol `#174E57`, terracota `#D9773F`,
Fraunces + Inter, alvos de toque ≥ 44 px, foco com anel de 3 px, sem preto ou
branco puros. Toda copy em pt-BR, sem dados de demonstração rotulados como
reais.

## Objetivo

Fazer do `/registrar` um fluxo de **captura primeiro, conta depois**: o
formulário funciona na chegada, o esforço é salvo no aparelho e a conta só é
pedida no momento de salvar no diário. As condições escolhidas em `/condicoes`
valem mais que um rascunho antigo, sem apagar o que a pessoa já digitou.

## Não-objetivos

- Criar conta automática, auto-submit ou login em segundo plano.
- Sincronizar rascunho com a nuvem, fila offline ou outro aparelho.
- Mexer na API, no modelo de dados ou no contrato OpenAPI.
- Tocar em `/entrar`, `/aceite` ou na política de limpeza de logout explícito.
- Redesenhar as quatro etapas do formulário; a mudança é de disponibilidade,
  persistência e precedência, não de arquitetura de telas.

## Anti-objetivos

- Aviso que convida a preencher com `inert` no formulário.
- Redirecionar para `/entrar` ao tocar em “Foto” ou ao tentar salvar.
- Banner de rascunho que prometa sincronização, backup ou nuvem.
- Condições de link aplicadas sobre um rascunho sem aviso visível.
- Duas primárias visíveis ao mesmo tempo na barra de ação.

## Abordagens consideradas

1. **Manter o bloqueio e corrigir só a copy.** Barato e reversível, mas deixa
   a jornada invertida: ninguém experimenta o valor antes da conta. Não resolve
   #43 e contraria a decisão de produto já tomada. Descartada.
2. **Captura primeiro com rascunho local (escolhida).** O formulário abre ativo;
   `input`/`change` alimentam `fisga:rascunho:v1` para qualquer visitante, com
   flush ao sair da página. A conta entra como confirmação no submit e como
   explicação no campo de foto. Custo: mais um painel de confirmação e um
   indicador de rascunho, ambos no mesmo artefato do OpenDesign; sem mudança de
   API. É a única opção que resolve #43, #44 e #45 juntas e mantém a privacidade
   honesta (“neste aparelho”).
3. **Fila de sincronização offline / rascunho no servidor para anônimo.** Daria
   continuidade entre aparelhos, mas exige identidade antes do valor, novo
   contrato e conceito de anonimato persistente. Fora do escopo destas três
   issues e contra o princípio de pedir o mínimo. Fica para o futuro.

## Decisões de comportamento

### Formulário anônimo

- `bloquearFormulario()` deixa de existir. O aviso `#acesso-registrar` passa a
  ser um banner compacto, sempre visível para quem não está autenticado:

  ```
  [cadeado aberto] Você pode preencher agora. Entrar só quando quiser salvar. Entrar
  ```

- Um único link discreto “Entrar” aponta para
  `/entrar?voltar=<url atual>`. Não há modal, scrim nem “Conta necessária”.
- O formulário nunca recebe `inert` nem `form-bloqueado`. Navegação por teclado,
  leitor de tela e as quatro etapas funcionam igual sem sessão.
- A barra de ações mantém a linha de privacidade e uma única primária.

### Rascunho local

- `input` e `change` chamam `agendarRascunho()` independentemente de
  `autenticado`. O debounce continua em 500 ms.
- `salvarRascunho()` é chamado também em `pagehide` e `beforeunload` (flush),
  além de antes de qualquer navegação para `/entrar`.
- O indicador passa a existir no formulário, com `role="status"`:

  `✓ Rascunho salvo neste aparelho · HH:MM`

  Atualizado após cada gravação; quando não há rascunho, fica vazio. Nunca diz
  “sincronizado”, “na nuvem” ou “seguro no servidor”.
- `coletar()` passa a manter uma captura que tenha **qualquer** conteúdo:
  espécie, peso, comprimento, isca, `solto`, `fotoUrl`/`fotos` ou observação.
  `preencher()` já reconstrói cada linha; a restauração de captura só com isca,
  solto ou foto volta a funcionar.
- Erro ao ler `localStorage` não trava o fluxo: mostra
  `Não conseguimos recuperar o rascunho. Você pode continuar preenchendo.` e
  segue com formulário vazio.
- Logout explícito e sessão expirada (401) continuam apagando o rascunho, como
  hoje. Navegação anônima para `/registrar` preserva (`preservarRascunho: true`).

### Condições da URL versus rascunho

- `aplicarCondicoesDaUrl()` é separado em duas partes: busca/derivação das
  condições e aplicação. A busca continua exigindo `lat` e `lon`.
- Ordem de carregamento:

  1. ler o rascunho, se houver;
  2. se a URL trouxer `lat`/`lon`, buscar condições e montar um objeto com
     `local`, `cidade`, `estado`, `ambiente`, `data`, `periodo`, `mare`, `lua`,
     `clima` e `privacidadeDoLocal` (quando vier da URL/API);
  3. mesclar: os campos que a URL trouxe **vencem**; o rascunho completa apenas
     os campos que a URL não trouxe (`pescador`, `observacoes`, `capturas`,
     `clarezaDaAgua` se ausente, `privacidadeDoLocal` se ausente);
  4. `preencher(mesclado)` uma única vez;
  5. mostrar o banner “Condições aplicadas” com os valores aplicados e a
     explicação `Os valores do link vieram na frente do seu rascunho; o que você
     já tinha digitado continua aqui.`;
  6. aplicar `especie` da wiki por último.

- Critério de aceite de #45: chegar de `/condicoes` com rascunho existente faz
  data, período, maré, lua, clima e local refletirem a URL, o rascunho completa
  o resto e o banner aparece. Sem `lat`/`lon`, o comportamento atual de
  restaurar o rascunho integral permanece.

### Carregamento, edição e retorno do login

- `?editar=<id>` mantém o caminho atual e só é tratado com sessão autenticada:
  `iniciarEdicao()` carrega a pescaria salva e retorna antes de qualquer
  rascunho ou condição da URL. Nunca mesclamos rascunho ou condições sobre uma
  pescaria em edição, para não alterar um registro já salvo sem intenção.
- O formulário fica ativo desde a primeira pintura, enquanto `/api/auth/me` e
  `/api/condicoes` ainda estão pendentes. Para não apagar o que a pessoa digita
  nesse intervalo, a mesclagem respeita campos tocados: valores alterados antes
  da restauração são preservados. Se a pessoa já tiver criado ou editado uma
  linha de captura, as capturas do rascunho são acrescentadas, nunca
  substituídas.
- “A URL vence o rascunho” vale para o rascunho salvo, não para digitação ao
  vivo: se a pessoa escreveu em um campo antes das condições chegarem, o valor
  dela fica. O banner continua listando as condições aplicadas nos demais
  campos.
- Depois de entrar, `voltar` pode trazer `lat`/`lon`. Aplica-se a mesma regra:
  condições da URL vencem o rascunho também no retorno, e o banner aparece.
  Não há dois fluxos de carregamento: a diferença entre anônimo e autenticado
  é apenas a visibilidade do banner e o que acontece no submit.

### Foto sem sessão

- O controle “Foto” continua visível na linha de captura.
- Ao tocar nele sem sessão, **não** abre o seletor de arquivos e **não** navega.
  Aparece uma nota inline no próprio campo, com `role="status"`:

  `A foto entra depois que você entra. O resto do registro continua aqui.`

- O `input[type=file]` é limpo para não disparar `change` pendente. Com sessão,
  o fluxo de upload atual segue intacto.

### Salvar sem sessão

- No `submit` anônimo, **não** chamar `irParaEntrar()`. Abrir um painel no
  contexto, acima da barra de ação (não modal, sem scrim):

  ```
  ANTES DE SALVAR
  Seu registro está pronto.
  Para guardar no diário, entre com sua conta. O que você preencheu continua aqui, sem mudanças.
  [ Entrar e salvar no diário ]  [ Continuar aqui ]
  ```

- Enquanto o painel estiver aberto, a primária do rodapé fica oculta: só uma
  primária por vez. `role="status"` anuncia a abertura; o foco vai para a
  primária do painel.
- “Continuar aqui” fecha o painel, devolve o foco ao botão de salvar e mantém
  todos os valores e a etapa atual. Não salva no servidor.
- “Entrar e salvar no diário” faz flush do rascunho, guarda
  `fisga:retorno` e navega para `/entrar?voltar=<url atual>`. Depois do login,
  a volta a `/registrar` restaura o mesmo rascunho — com as condições da URL
  aplicadas por cima, se `voltar` ainda trouxer `lat`/`lon` —, o banner anônimo
  desaparece e o envio continua **explícito**: a pessoa clica em “Salvar
  pescaria”. Nunca auto-submit.
- Com sessão, o submit segue o fluxo atual (validação, upload, POST/PUT, limpeza
  do rascunho e ida para `/p/<token>`).

### Estados

anônimo padrão · digitando/rascunho salvo · condições aplicadas por link ·
nota de foto anônima · confirmação para salvar · depois do login · rascunho
vazio · erro de leitura do rascunho · salvando · salvo.

## Restrições

- Nenhuma dependência nova; nenhuma chamada de API nova; nenhuma alteração em
  `docs/api/openapi.json`.
- Manter os ids e atributos já usados por testes e pelo service worker
  (`#pescaria-form`, `#acesso-registrar`, `#condicoes-aplicadas`, `#btn-salvar`,
  `fisga:rascunho:v1`, `fisga:retorno`).
- Não tocar na distinção `fisga-public-*` / `fisga-private-*` nem no precache.
- Acessibilidade: foco visível, `aria-live` nas mudanças de status, alvos ≥ 44
  px, um `h1` por rota, sem cor como único sinal.
- Copy em pt-BR; sem exclamações, emoji ou palavras em inglês na interface.
- Documentação do comportamento atualizada no mesmo PR e indexada em
  `docs/INDEX.md`.

## O que fica para o construtor

- A composição final do banner, do indicador de rascunho, da nota de foto e do
  painel de confirmação, guiada pelo artefato do OpenDesign e pelos tokens
  existentes — sem introduzir estética paralela.
- Ajustes de responsividade do painel abaixo de 390 px, mantendo a primária
  larga no mobile e a linha de privacidade à esquerda no desktop.
- Os testes que descrevem o comportamento antigo (`registrar anônimo preserva o
  rascunho e bloqueia o formulário`) devem ser reescritos, não apenas
  desabilitados: o rascunho continua preservado, mas o formulário deixa de ser
  bloqueado.
