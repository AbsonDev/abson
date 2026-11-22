# Fisga — F2.1: esqueleto do portal e gerador da referência

**Data:** 21/11/2026
**Status:** aprovado
**Issue:** [#73](https://github.com/AbsonDev/abson/issues/73) (fase F2 do programa #58)
**Depende de:** F1 concluída (`1e01091`) — contrato com `info`, `security`,
`x-rate-limit`, `x-cache`, status reais e `operationId` estável

## Intenção

A F1 deixou o contrato verdadeiro; ele ainda não é legível por quem integra.
A F2.1 cria o portal estático `docs.fisga.abson.tech` no repositório: o
esqueleto Astro com o design system do Fisga e o gerador que transforma
`docs/api/openapi.json` em páginas de referência. Sem prosa de conceito (F2.2),
sem busca (F2.3), sem CI completo (F2.4), sem deploy (F2.5) e sem console (F3.1).

A pergunta que a fase responde: **o contrato, sozinho, produz uma página que
quem integra consegue usar?** O que ele não sabe — o exemplo que roda, o que
verificar, de quem é o dado — é escrito à mão em um arquivo por operação, e o
build falha quando falta.

## Objetivos

- `Projetos/fisga/docs-site/` em Astro 7 estático (`output: 'static'`), sem
  adaptador, sem runtime de terceiros.
- `scripts/gerar-referencia.mjs` lê o contrato e os overrides, valida e emite o
  modelo da referência consumido pelas páginas.
- 54 páginas de operação, uma por `operationId`, em
  `/referencia/<dominio>/<operationId>`, mais `/referencia` e 13 índices de
  domínio.
- Build falha nas sete condições listadas adiante.
- Layout base com os tokens do pacote `design-system/fisga/` — nenhum valor de
  cor, tipo ou espaçamento redeclarado.

## Não-objetivos

- Prosa de conceito, guia rápido executável e glossário (F2.2).
- Pagefind, `sitemap.xml`, `robots.txt`, revisão AA completa (F2.3).
- `astro check` no CI, `lychee` no `dist/`, orçamento de tempo do job (F2.4).
- Coolify, DNS, `/health.txt`, `deploy-docs` (F2.5).
- Console "Experimente" e CORS (F3.1).
- Mudar o contrato ou o código da API. O portal **consome** o `openapi.json`.

## Restrições

- `openapi.json` é artefato: o portal lê, nunca reescreve.
- UI e copy em pt-BR; termos de API em inglês onde são o nome real.
- Nada de segredo, token ou PII nos exemplos.
- O contrato continua sendo a fonte de verdade: prosa não inventa rota, status
  ou `codigo`.
- `gate` segue sendo o único check obrigatório.

## O que o contrato entrega hoje (medido)

Números levantados no `openapi.json` desta árvore, não estimados:

| Fato | Medida |
| --- | --- |
| Operações | 54 em 48 rotas, 13 tags, todas com `operationId`/`summary`/`description` |
| Exatamente 1 tag por operação | 54 de 54 |
| `security` | 29 `cookieAuth`, 2 `adminToken`, 23 vazias |
| `x-rate-limit` | presente em 50; ausente em `/health`, `/ready`, `enviarNewsletter`, `previsualizarNewsletter` |
| `x-cache` | presente em 54 |
| `requestBody` | 20, todos `required: true`; 18 JSON, 2 `multipart/form-data` |
| `example`/`examples` | 1 no documento inteiro (`GET /api/auth/me`); 0 nos 20 corpos |
| `description` em propriedade de schema | 0 de 327 |
| Status `>= 400` | 53 operações; só `/health` não tem nenhum |
| Sem `2XX` (desfecho é `302`) | 6: `confirmarLink`, `iniciarLoginGoogle`, `concluirLoginGoogle`, `conectarGoogle`, `concluirConexaoGoogle`, `descadastrarNewsletter` |
| `302` com header `Location` declarado | 0 — o destino só existe na prosa da operação |
| `403` citando `consentimento_pendente` | exatamente as 28 operações com `cookieAuth` fora da allowlist do middleware |
| Tipos compostos (documento inteiro) | 49 `["number","string"]` (decimal serializado como string), 71 `["null","string"]`, 18 `["integer","string"]`, 7 `["null","number","string"]`, 3 `["array","null"]` |
| `oneOf` | 4, todas do mesmo formato — objeto anulável (`oneOf: [{type: null}, {$ref}]`): `CondicoesDto.melhorJanela`, `CondicoesDto.mare`, `MareDto.proximoEvento`, `MeResponse.usuario` |
| `allOf`/`anyOf`/`additionalProperties`/ciclos | 0 |
| `enum` | 9 schemas; vários começam com sentinela (`NaoInformada`, `NaoInformado`, `NaoSeAplica`) |
| Sucesso sem corpo | 8 `204`; 6 `302`, nenhum com `Location` declarado |
| Sucesso com `Location` | 7 (`201` e `202`), declarado no contrato |
| Resposta de raiz array | 5 (`listarSessoes`, `buscarLocais`, `listarLocais`, `listarPontos`, `listarTrilhas`) |
| `required` que aceita `null` | 41 pares (operação, propriedade) em resposta, em 17 operações: 38 `["null", X]`, 3 `oneOf`; request: 34 pares em 16 operações |
| Linhas de erro deriváveis | 115 de 152 pares (operação, status): 61 nomeiam o `codigo`, 54 declaram corpo vazio (50 `429`, 3 `404`, 1 `503`) |
| Linhas de erro que exigem juízo | 37 pares com corpo e sem `codigo`: 23 `400`, 12 `404`, 2 `503` — em 33 operações (29 com uma linha, 4 com duas) |
| Request sem `required` | 1 schema, 2 operações (`EntradaDePescaria`, 15 propriedades, usado por `criarPescaria` e `atualizarPescaria`) |

Consequências diretas: (a) exemplos de request **têm** que vir do override, não
há de onde tirá-los; (b) a página não pode prometer `Location` no `302`;
(c) o sintetizador de resposta precisa entender tipo composto, `oneOf` anulável
e sentinela de `enum`; (d) consentimento é derivável do próprio contrato;
(e) a tabela de erros é majoritariamente derivável — o override só escreve o que
o contrato cala.

## Abordagens consideradas

**A — exemplos no contrato.** Transformers emitem `example` por schema e
`requestBody`; o portal só renderiza. Verdade única, e o exemplo viaja para
quem consome o `openapi.json`. Desvantagem: reabre a F1 recém-fechada, engorda
um artefato público com dado de exemplo e põe decisão editorial (o que é um
exemplo plausível e sem PII) dentro do build da API.

**B — exemplos no override, exigidos pelo gerador (escolhida).** O contrato dá
tudo que é mecânico; o override declara o que exige juízo humano — o `curl` que
roda, o que verificar, os relacionados, os erros com `codigo`. O gerador falha
sem eles. Vantagem: F1 fica fechada, o exemplo é revisado como texto no PR e o
contrato não vira depósito de dados inventados. Desvantagem: 54 arquivos novos
para escrever.

**C — gerador deriva exemplo do schema.** `uuid` → `00000000-…`, `string` →
`"texto"`, marcado como ilustrativo. Barato, mas quebra a regra 2 da
[política editorial](../../api/politica-editorial.md) ("todo bloco de código é
copiável e executável") e produz um exemplo que mente sobre formato (token,
envelope, campos nulos).

**Decidido com o humano:** B para request e `curl`; resposta de sucesso é
derivada do schema e rotulada como ilustrativa.

## Decisões de desenho

### URL: uma operação por página

`/referencia/<slug-do-dominio>/<operationId>`, sem barra final. Decidido com o
humano contra a alternativa de 13 páginas com âncora interna.

- `slug-do-dominio` sai da tag do contrato, sem acento: `Autenticação` →
  `autenticacao`. Derivado, não versionado: tag nova entra sozinha.
- `operationId` já é congelado no contrato e tem teste; é a chave de URL e de
  arquivo de override.
- Páginas geradas: `/` + `/referencia` + 13 domínios + 54 operações = **69**.

### Rotas de arquivo que produzem essas URLs

Com `build.format: 'file'`, `index.astro` vira `<dir>/index.html` e um arquivo
`nome.astro` vira `nome.html`. Para não depender de o servidor resolver
`/referencia` como diretório:

```text
src/pages/
├── index.astro                      → index.html
├── referencia.astro                 → referencia.html
└── referencia/
    ├── [dominio].astro              → referencia/<dominio>.html
    └── [dominio]/[operacao].astro   → referencia/<dominio>/<operationId>.html
```

Nada de `referencia/index.astro`: com `format: 'file'` ele também emite
`referencia.html` (medido), e ter os dois arquivos gera conflito de rota no build.
Um arquivo por URL, e `referencia.astro` é o que corresponde ao formato `file`.

### Ordem canônica: a do mapa de URL do `portal.md`

A ordem do array `tags` do contrato **não é contrato**: o
`ContratoDocumentTransformer` diz no código que a ordem vem do gerador. Hoje
sai `Sistema · Autenticação · Conta · Newsletter · Catálogo · Diário · Mídia ·
Condições · Locais · Espécies · Pescarias · Pontos · Trilhas`, que é a ordem de
primeira aparição em `paths`, não a de leitura. A navegação usa a do
[mapa de URL](../../api/portal.md#mapa-de-url), que é a que o protótipo validou:

```text
sistema · catalogo · autenticacao · conta · pescarias · diario ·
condicoes · locais · pontos · trilhas · midia · especies · newsletter
```

- A lista vive em uma constante do gerador, com comentário apontando o
  `portal.md`.
- Um teste falha se o contrato ganhar tag sem entrada na lista ou se a lista
  tiver entrada sem tag.
- Dentro do domínio, as operações seguem a ordem de `paths` do contrato.
- Título do domínio = `name` da tag (`Autenticação`, `Sistema`). O
  [inventário](../../api/inventario.md) usa nomes longos ("Autenticação &
  Sessão", "Sistema & Legal", "Visão/Espécies") nas duas tabelas de resumo; as
  URLs do `portal.md` já usam o slug curto. O inventário é corrigido para o
  nome da tag no mesmo PR, porque duas fontes para o mesmo título divergem. A
  descrição do domínio continua vindo de `tag.description`.

### Modelo: JSON gerado, não content collection

O gerador valida e emite `docs-site/src/data/referencia.json` (gitignorado);
as páginas são `.astro` com `getStaticPaths` lendo esse JSON. O conteúdo não é
escrito à mão — é derivado — e o formato do modelo interessa ao gerador e às
páginas, não ao Astro. Content collection fica para prosa humana na F2.2.

- `predev`, `prebuild`, `precheck` e `pretest:unit` rodam o gerador, então editar
  override reflete no servidor de desenvolvimento, `astro check` e os testes de
  unidade não quebram em clone novo. `test:paginas` não depende dele: lê `dist/`.
- O JSON carrega o modelo já resolvido (`$ref` expandido, tipos compostos
  normalizados), para as páginas não precisarem de um resolver de OpenAPI.

### Contrato do override

`docs/publico/operacoes/<operationId>.md`, um por operação. Tudo que é
estruturado fica no frontmatter; o corpo é prosa livre.

```yaml
---
operacao: criarPescaria            # obrigatório; existe no contrato
proposta: >                        # obrigatório; 1–3 frases, o efeito
  Cria uma pescaria no diário do usuário e devolve o link público.
acesso: sessao                     # publico | capacidade | sessao | dono | admin
corpo: |                           # JSON, quando o contrato declara requestBody JSON
  {"local":"Represa de Guarapiranga","ambiente":"Represa","data":"2026-11-20","periodo":"Manha"}
verificar: >                       # obrigatório
  GET /api/pescarias/{shareToken} devolve a pescaria criada.
relacionados: [obterPescariaPorToken, atualizarPescaria]   # opcional
erros:                             # só o que o contrato não nomeia
  - status: 400
    codigos: [data_invalida, local_longo, ambiente_invalido]
    quando: Um campo do corpo não passou na validação; o `codigo` diz qual.
---

A pescaria nasce privada; o `shareToken` devolvido é o que permite compartilhar.
```

Operação que usa os outros campos:

```yaml
---
operacao: obterCondicoes
proposta: >
  Devolve clima, maré aproximada e a melhor janela do dia para as coordenadas.
acesso: publico
consulta: {lat: -23.55, lon: -46.63, nome: "Represa de Guarapiranga"}
verificar: >
  O corpo traz `temMare`, `agora` e `tempo`; sem `lat`/`lon` a resposta é `400`.
erros:
  - status: 400
    codigos: [local_sem_coordenadas, latitude_invalida, longitude_invalida]
    quando: Faltam `lat`/`lon` ou a coordenada está fora da faixa válida.
---
```

Rota com path param declara `caminho`, e só ela: `caminho: {id: 00000000-0000-4000-8000-000000000001}`.

**Por que a tabela de erros está no frontmatter, e não numa tabela Markdown no
corpo.** Medido: o markdownlint trata o `---` de abertura como conteúdo quando
o arquivo não tem corpo (erros `MD007`/`MD032` dentro do YAML), e a tabela em
Markdown exigiria um parser de tabela no gerador, com escape de `|` em prosa.
Estruturado, a checagem "status do spec tem linha" vira comparação de conjuntos
e o revisor vê a lista no diff. O corpo fica só com o que é prosa.

Regras dos campos:

- `caminho` é obrigatório quando a rota tem `{param}` e proibido quando não tem;
  cada chave existe em `parameters`, e o valor é concreto (`id` é UUID válido,
  `shareToken` é token inventado de formato plausível).
- `consulta` só aceita chave que existe em `parameters` com `in: query`. Nenhum
  query param do contrato é `required`, mas três operações devolvem `400` sem
  valor — `obterCondicoes` e `compararCondicoes` (`local_sem_coordenadas`) e
  `reverterCoordenadas` (`coordenadas_invalidas`) —, e `buscarLocais` sem `q`
  devolve lista vazia. O gerador **não** adivinha: se a operação precisa de
  valor para chegar a um resultado útil, o override declara.
- `corpo` é obrigatório com `requestBody` JSON e proibido sem ele. JSON válido,
  com todas as propriedades `required` do schema e sem PII. O único schema de
  request sem `required` é `EntradaDePescaria` (usado por `criarPescaria` e
  `atualizarPescaria`): ali o override escreve o corpo mínimo que chega a um
  `201`, e a tabela marca todos os campos como opcionais — o gerador não inventa
  obrigatoriedade que o contrato não declara.
- `arquivos` é obrigatório com `multipart/form-data`: mapeia o campo do schema
  (`arquivo` em `FormularioDeFoto`) para o nome de arquivo usado em `-F`.
  `enviarMidia` e `sugerirEspecies` não têm `corpo`.
- `erros` é **exceção, não espelho**. A tabela é montada assim, por par
  (operação, status `>= 400`):
  1. se a `description` do contrato nomeia o `codigo` entre crases (`401`, `403`,
     `500`), a linha vem do contrato — 61 pares;
  2. se a `description` diz "Corpo vazio" (`429`, os três `404` sem corpo —
     `enviarNewsletter`, `previsualizarNewsletter` e `reverterCoordenadas` — e o
     `503` de `/ready`), a linha vem do contrato, com `codigo: —` — 54 pares;
  3. nos 37 pares restantes (`23× 400`, `12× 404`, `2× 503`), o contrato só diz o
     status; o override declara `codigos` e `quando`, porque é o único lugar onde
     essa informação existe.

  O override **não pode** repetir o que o contrato já nomeia; se repetir, o build
  falha com "linha derivável do contrato". Status que aparece nos dois grupos
  precisa de todas as linhas: um `400` derivado do contrato não é o mesmo `400`
  que o handler devolve com `data_invalida`.
- `erros[].codigos` é lista: o `403` tem até três, o `400` de validação tem
  dezenas possíveis. O override lista os que a operação realmente devolve; a
  página acrescenta a frase de contexto ("o `codigo` diz qual campo"), não um
  catálogo completo — o catálogo é `/referencia/erros`, na F2.2.
- `erros[].codigo` singular fica aceito como atalho para lista de um item.
- O corpo do arquivo **não tem heading**: as seções da página são fixas pelo
  template e o corpo é renderizado dentro da seção "Comportamento e efeitos".
  O gerador falha se uma linha fora de bloco de código começar com `#`.
  Parágrafos, listas, código inline e links são permitidos; o gerador renderiza
  com `marked`. Corpo vazio é válido — nem toda operação tem o que acrescentar.
- Nenhum campo que o contrato já sabe entra no override: título, método, rota,
  resumo, descrição, acesso técnico (`security`), consentimento, rate limit,
  cache, parâmetros, schemas e status. Chave desconhecida no frontmatter é
  falha de build.

### Autenticação × autorização: contrato autentica, override autoriza

O contrato diz **se** a operação exige sessão (`security`) e nada diz sobre
**de quem** é o dado. `GET /api/pescarias/{id}/editar` e
`GET /api/pescarias/{shareToken}` têm `security` opostas, mas o segundo é
leitura por capacidade e o primeiro é do dono. Derivar isso de path param seria
heurística, e a página existe justamente para dizer quem pode chamar.

| `acesso` | `security` exigido pelo contrato | Quantas hoje |
| --- | --- | --- |
| `publico` | vazio | 18 |
| `capacidade` | vazio, e a rota tem `shareToken` ou token na query | 5 |
| `sessao` | `cookieAuth` | 29 no total, divididas com `dono` |
| `dono` | `cookieAuth`, e o `id`/token identifica recurso de alguém | idem |
| `admin` | `adminToken` | 2 |

`publico` e `capacidade` exigem `security` vazio; `sessao`/`dono`, `cookieAuth`;
`admin`, `adminToken`. O que o contrato não resolve é `sessao` × `dono` e
`publico` × `capacidade` — é aí que o override decide, e o build só valida a
compatibilidade com `security`, não a divisão. O palpite natural para `dono` são
as 9 com `{id}`, mas é o override que afirma.

Duas operações são `publico` e ainda assim leem a sessão quando ela existe:
`obterSessao` (devolve `autenticado: false` sem cookie) e `sair` (`204` sempre).
O contrato já diz isso na `description`; a página não troca o aviso de sessão por
causa disso, porque o `curl` funciona sem cookie.

`acesso` incompatível com `security` falha o build. A página mostra os dois
separados: "Autenticação: sessão" (do contrato) e "Quem pode chamar: o dono da
pescaria" (do override).

### Consentimento sai do contrato, não de uma cópia

Medido: as 28 operações cujo `403` cita `consentimento_pendente` são exatamente
as 29 com `cookieAuth` menos `POST /api/conta/consentimento`. As outras duas
rotas da allowlist do middleware (`GET /api/auth/me` e `POST /api/auth/sair`) já
têm `security` vazio, então nem entram na conta. O gerador deriva consentimento
do contrato — não mantém uma allowlist própria que pode envelhecer em silêncio.

O teste do gerador fixa a allowlist de três rotas do middleware e compara
`cookieAuth` menos a interseção com ela (`registrarConsentimento`) com o conjunto
derivado do `403`, que dá 28. Se a API mudar o middleware, o contrato muda, o
derivado muda e o teste falha com a lista na mão — em vez de o portal continuar
dizendo a coisa errada.

### Exemplo executável com sessão: pré-requisito honesto

29 operações exigem `cookieAuth`. O `curl` usa `-b cookies.txt`, e a política
editorial só admite cookie "depois de um passo que grava o cookie" — que é a
primeira chamada do guia rápido, ainda inexistente (F2.2).

Solução: `PreRequisitoDeSessao.astro`, renderizado antes do bloco quando
`security` tem `cookieAuth`, com copy fixa:

> **Antes de chamar.** Estes exemplos usam o cookie `fisga_auth` salvo em
> `cookies.txt`. Se você ainda não tem, faça `POST /api/auth/entrar` com
> `curl -c cookies.txt` antes de rodar o exemplo abaixo.

O link aponta para a página gerada de `entrarComSenha`, que mostra o
`curl -c cookies.txt` completo. Nada de texto duplicado, nada de exemplo que não
roda. As duas administrativas exigem `-H "X-Admin-Token: SEU_ADMIN_TOKEN"` no
lugar do cookie.

### Resposta de sucesso: derivada, rotulada

O contrato não tem exemplo de resposta. O gerador sintetiza um **esqueleto do
schema**, determinístico, e a página rotula: "Exemplo derivado do schema —
mostra a forma, não dados reais."

Regras do sintetizador, todas motivadas pelo que foi medido:

| Caso no contrato | Saída |
| --- | --- |
| `example`/`examples` no media type | o exemplo do contrato, verbatim (hoje só `obterSessao`) |
| `uuid` | `00000000-0000-4000-8000-000000000000` |
| `date` / `date-time` | `2026-11-21` / `2026-11-21T09:00:00Z` |
| `uri` | `https://fisga.abson.tech/exemplo` |
| `["number","string"]` / `["integer","string"]` | número (`0`), não string |
| `["null", X]` ou `oneOf: [null, X]` | o esqueleto de `X`, com a legenda "este campo pode ser `null`" |
| `required` que aceita `null` (41 pares em resposta) | o esqueleto do tipo, nunca `null` — o exemplo mostra a forma; a página avisa que o contrato permite `null` |
| `enum` | primeiro valor que não comece com `Nao`; se todos começarem, o primeiro |
| `binary` | não aparece em resposta; sem bloco |
| `object` | todas as propriedades, na ordem do schema |
| `array` | `[]`, com nota do que apareceria ali (5 respostas têm raiz `array`) |
| `text/html`, `application/pdf` | sem bloco |
| `204` | sem bloco: "Resposta sem corpo." |
| `201`/`202` com `Location` declarado | bloco do corpo + o header `Location` na linha de status |

Nunca inventa dado plausível: os campos de texto livre (`local`, `pescador`)
saem `""`/`null`, porque preencher com nome de pessoa é PII inventada.

**Conflito com a política editorial, resolvido na F2.1.** A seção 6 da
[política editorial](../../api/politica-editorial.md#estrutura-obrigatória-de-uma-página-de-operação)
pede "exemplo de resposta completo". A F2.1 não consegue entregar isso sem
inventar dado: o contrato não tem exemplo e não existe gravação de resposta real
nesta fase. Decidido: a página mostra o esqueleto com o rótulo explícito e a
política editorial ganha uma nota de que, na F2.1, a seção 6 é o esqueleto
derivado; o exemplo real entra na F2.2, quando o guia rápido grava as respostas.
Não existe `resposta:` no override nesta fase — aceitar agora um campo sem
formato definido colidiria com a regra de não redeclarar o contrato.

### A página da operação

As 11 seções da [política editorial](../../api/politica-editorial.md#estrutura-obrigatória-de-uma-página-de-operação),
alimentadas assim:

| # | Seção | Fonte |
| --- | --- | --- |
| 1 | Título e verbo | `summary` + método + rota |
| 2 | Propósito | override `proposta` |
| 3 | Acesso | `security` + override `acesso` + consentimento derivado |
| 4 | Limites | `x-rate-limit` + `x-cache` |
| 5 | Request | `parameters` + `requestBody`, com os valores do override |
| 6 | Sucesso | status `2XX`/`3XX` + esqueleto do schema |
| 7 | Erros | derivado da `description` + `erros` do override (só o que o contrato cala) |
| 8 | Exemplo executável | override + método/rota/`servers[0]` |
| 9 | Como verificar | override `verificar` |
| 10 | Comportamento e efeitos | `description` do contrato + notas do corpo |
| 11 | Relacionados | override `relacionados` |

Casos que o template precisa cobrir, medidos:

- **Sem `x-rate-limit`** (4 operações): a linha de limites diz "sem política de
  rate limit declarada" em vez de sumir.
- **Sem `2XX`** (6 operações): a seção 6 vira "Desfecho: `302`" e
  mostra o que a `description` diz sobre o destino e o cookie. O contrato **não**
  declara `Location`, então a página não inventa URL.
- **Sem `2XX` nem `3XX`**: não existe no contrato.
- **Sem status `>= 400`** (`/health`): a seção 7 diz "Sem erro declarado no
  contrato", sem tabela vazia.
- **`text/html` e `application/pdf`** (`previsualizarNewsletter`,
  `exportarPescariaPdf`): sem bloco de resposta, com o media type declarado.
- **`multipart`** (`enviarMidia`, `sugerirEspecies`): o exemplo usa
  `-F "arquivo=@foto.jpg"`, e o bloco de request mostra o campo do schema, não
  um JSON.
- **Path param sem descrição** (28 dos 31 parâmetros não têm `description`): a
  linha da tabela mostra nome, local, tipo e obrigatoriedade; a explicação fica
  na prosa do override quando importa.

### Home provisória

`/` na F2.1 é derivada de `info`: título, descrição, versão do contrato, os dois
caminhos (`/referencia` e a futura `/guia-rapido`, desabilitada com aviso) e a
faixa "Leitura pública. Escrita exige sua conta." O template editorial completo
(três domínios mais usados, aviso de privacidade, estado da API) é F2.2.

O `README.md` do `docs-site` marca a home como provisória, e
`verificar-paginas.test.mjs` cobre as 69 páginas com as asserções do seu tipo.

### As sete falhas de build

O gerador sai com código 1 e mensagem com arquivo, operação e campo quando:

1. operação do contrato sem override;
2. override com `operacao` que não existe no contrato, ou repetida em dois
   arquivos;
3. par (operação, status) `>= 400` que o contrato **não** resolve — tem corpo e a
   `description` não nomeia nenhum `codigo` — e não tem linha em `erros`;
4. linha em `erros` que o contrato **já** resolve (nomeia o `codigo` ou diz
   "Corpo vazio"), ou com status que o spec não declara `>= 400`;
5. `requestBody` JSON sem `corpo`, `multipart` sem `arquivos`, ou `corpo`/
   `arquivos` presentes sem o `requestBody` correspondente;
6. `caminho`/`consulta` com chave fora de `parameters`, com chave faltando
   quando exigida, ou presente quando a rota não tem o parâmetro;
7. `relacionados` com `operationId` inexistente, `acesso` incompatível com
   `security`, ou chave desconhecida no frontmatter.

A 6 e a 7 agrupam o que a arquitetura não listava e que apareceu ao modelar o
override. A 6 é a que impede o exemplo que devolve `400` — o caso
`obterCondicoes` sem `lat`/`lon`. A 4 é o que impede o override de virar uma
segunda fonte de verdade para o que o contrato já diz.

### Build e apresentação

- `output: 'static'`, `trailingSlash: 'never'`, `build.format: 'file'`.
  Documentado no Astro: `file` gera `/rota.html` e é o par de `never`
  (`directory` pede `always`).
- **Consequência para a F2.5:** o host estático precisa resolver
  `/referencia/conta/listarSessoes` para `referencia/conta/listarSessoes.html`.
  O Coolify permite configurar nginx no app estático; a regra
  (`try_files $uri $uri.html $uri/ =404`) entra no runbook da F2.5, não aqui.
- Tokens: `prebuild` copia `design-system/fisga/tokens.css` para
  `src/styles/gerado/tokens.css` (gitignorado). Uma fonte de verdade, nenhum
  valor redeclarado, nenhum `../..` saindo do `root` do Vite.
- **Fontes (medido no Astro 7.4.13, não suposto):** importar
  `@fontsource-variable/fraunces/standard.css` + `inter/wght.css` emite **as 10
  variantes de subset** (372 KB em `_astro/`), porque o CSS do pacote declara
  todos os `unicode-range`. Declarar `@font-face` próprio apontando para
  `@fontsource-variable/*/files/*-latin-*.woff2` emite **exatamente 2 arquivos**:
  `fraunces-latin-standard-normal.woff2` (67,3 KB, eixos `opsz` 9–144 + `wght`)
  e `inter-latin-wght-normal.woff2` (48,3 KB) — **115,6 KB** no total, dentro do
  orçamento de ~150 KB. `fraunces/wght.css` é menor (36,6 KB) porque **descarta**
  o eixo óptico, que o `DESIGN.md` exige ("Fraunces 500/600, optical sizing on");
  fica de fora por isso, não por tamanho. Nada de fonte de terceiro
  (`portal.md`), nada de Google Fonts. Subset latin cobre o pt-BR (U+0000–00FF).
- **Botão copiar e CSP (medido no Astro 7.4.13):** o `portal.md` fixa
  `script-src 'self'`, sem `'unsafe-inline'`. Num projeto de teste com
  `format: 'file'`, um `<script>` processado pelo Astro saiu **inline**
  (`<script type="module">…</script>`), que a CSP bloqueia em produção sem
  sintoma no build. Com `vite.build.assetsInlineLimit: 0` ele sai como módulo em
  `_astro/*.js` e a CSP vale como está. O config leva essa opção e um teste
  pós-build falha se algum HTML tiver `<script>` sem `src`. `is:inline` fica
  proibido no projeto.
- Componentes: `Base.astro` (head, skip link, header, footer, versão do
  contrato), `Nav.astro`, `PreRequisitoDeSessao.astro`, `BlocoDeCodigo.astro`
  (rótulo + copiar acessível), `TabelaDeErros.astro`, `SeloDeStatus.astro`,
  `TabelaDeSchema.astro`.

### CI neste PR: o mínimo que não deixa código sem teste

O `portal.md` põe o job `docs-site` na F2.4, mas publicar `docs-site/` sem
nenhum job deixa o PR com `gate` verde e o site nunca buildado. Neste PR:

- `docs-site/**` sai do filtro `CODIGO` **nos dois workflows** (`fisga-pipeline`
  e `fisga-deploy`). Medido: hoje `^Projetos/fisga/` casa com
  `docs-site/package.json` e rodaria `dotnet build` à toa; no deploy, dispararia
  `deploy-api` e `deploy-web` por causa de uma página de documentação.
- output `portal` no job `changes`, alimentado por `docs-site/**`,
  `docs/publico/**` e `docs/api/openapi.json`.
- job `docs-site`: `npm ci` → `npm run test:unit` → `npm run build` →
  `npm run test:paginas`. Os quatro scripts existem no `package.json` porque a
  ordem importa: os testes de página leem `dist/` e falham se rodarem antes do
  build, e `node --test` sem argumento não distingue os dois conjuntos. O
  gerador roda em `pretest:unit` e em `prebuild`; nenhum passo do CI o chama
  direto, então não há execução duplicada.
- `gate` passa a depender de `docs-site`.
- `docs-site/README.md` entra no glob do markdownlint e na regex de
  `DOCUMENTACAO` (`^Projetos/fisga/docs-site/.*\.md$`); sem isso, o único
  Markdown que sobra fora do lint seria justamente o que explica como rodar o
  site.

Isso muda um invariante registrado no `AGENTS.md` do Fisga ("documentação não
builda nem deploya"). A partir daqui, PR que toca `docs-site/**`,
`docs/publico/**` ou o contrato builda o portal no CI — e continua **sem**
disparar `deploy-api`/`deploy-web`, porque o filtro de deploy também exclui
`docs-site/**`. O texto do invariante é atualizado no mesmo PR, junto com o
`docs/ci-cd.md`.

Incluir `openapi.json` desde já fecha o buraco que o programa combate: PR de
contrato roda `api`, mas sem override a operação nova entraria na `main` com o
portal quebrado até o próximo PR de docs — o contrário de "quebrar no PR".
F2.4 amplia o job com `astro check`, `lychee` no `dist/`, o output para o deploy
e o orçamento de tempo.

## Estrutura de arquivos

```text
Projetos/fisga/docs-site/
├── package.json                 # astro, @astrojs/check, typescript, fontsource, yaml, marked
├── package-lock.json
├── astro.config.mjs             # static, site, trailingSlash never, format file
├── tsconfig.json                # extends astro/tsconfigs/strict
├── README.md                    # como rodar, como adicionar operação, home provisória
├── scripts/
│   ├── gerar-referencia.mjs
│   ├── gerar-referencia.test.mjs
│   └── verificar-paginas.test.mjs
└── src/
    ├── data/referencia.json     # gerado, gitignorado
    ├── styles/gerado/tokens.css # copiado do design-system, gitignorado
    ├── styles/portal.css
    ├── components/{Base,Nav,PreRequisitoDeSessao,BlocoDeCodigo,
    │               TabelaDeErros,SeloDeStatus,TabelaDeSchema}.astro
    └── pages/
        ├── index.astro
        ├── referencia.astro
        └── referencia/
            ├── [dominio].astro
            └── [dominio]/[operacao].astro
Projetos/fisga/docs/publico/operacoes/<operationId>.md        # 54 overrides
```

O override de `MD041` fica no `.markdownlint-cli2.jsonc` **da raiz**, na chave
`overrides` (`filter` + `config` + `combine: merge`), apontando para
`Projetos/fisga/docs/publico/operacoes/**/*.md` com
`front_matter_title: "^\\s*proposta\\s*[:=]"`. Medido: sobrevive ao `ignores` de
`docs/superpowers/**` e ao `config` global de `MD013`; um arquivo sem `proposta`
falha, um com `proposta` em bloco (`>`) passa. Não existe `.markdownlint-cli2.jsonc`
aninhado — o `overrides` da raiz cobre o caso sem criar uma segunda configuração.

## Testes

- `gerar-referencia.test.mjs` — uma teoria por falha (1–7); a linha de erro
  derivada × a que exige override (as 7 combinações de `corpo` × `nomeia
  codigo` × `declara vazio`); coerência `acesso` × `security` nas cinco
  combinações; consentimento derivado igual a `cookieAuth` menos as três
  isenções; slug de domínio; ordem canônica nos dois sentidos; `2XX`/`3XX` como
  desfecho e `302` fora de `erros`; sintetizador por tipo composto, `oneOf`
  anulável, `required` anulável e `enum` com sentinela; e o caminho feliz contra
  o contrato real (54 operações, 13 domínios, 69 páginas).
- `verificar-paginas.test.mjs` (pós-build) — para cada um dos 69 HTML: existe,
  um `h1`, `title`, âncora do `operationId` em operação, bloco de exemplo,
  tabela de erro quando o contrato ou o override declara `>= 400`, aviso de
  sessão nas 29 com `cookieAuth`, nenhum `<script>` sem `src`, e nenhuma cor
  literal fora de `tokens.css`.
- `npm run build` local e no job novo; `astro check` roda localmente neste PR.
- markdownlint nos 54 overrides (o glob já cobre `docs/**`), `verificar-rotas.mjs`
  segue 54/54/54.

## Riscos

- **Ordem canônica em constante manual.** Mitigação: teste de cobertura nos dois
  sentidos; tag nova obriga a decisão.
- **Consentimento derivado de texto de `description`.** Se alguém reescrever o
  `403` sem o código, o portal para de dizer "consentimento". Mitigação: o teste
  fixa a allowlist de três rotas e a contagem 28; a descrição é gerada por
  `RespostasPadrao`, um dono só.
- **54 overrides escritos de uma vez.** Mitigação: 115 dos 152 pares
  (operação, status) saem do contrato; o humano escreve só 37 linhas de erro, o
  request executável e a prosa. O gerador aponta campo por operação, e a tabela
  de erro derivada já vem pronta do inventário.
- **`erros` declarado no override pode envelhecer.** Se a API trocar o `codigo`
  que o handler devolve, o override continua dizendo o antigo e nada falha. A
  mitigação é de processo, não de build: o PR que muda `codigo` no código roda
  `api` (o job de contrato) e o checklist de review da política editorial manda
  procurar o `codigo` em `docs/publico/operacoes/`. Automatizar exigiria varrer
  os literais do C#, e um guard de caminho frágil já foi rejeitado no F1.3.
- **`build.format: 'file'` depende do nginx da F2.5.** Medido no build deste PR;
  a regra de `try_files` fica registrada no runbook da F2.5.
- **Fonte self-hosted**: 115,6 KB nos dois subsets latin, medido no build de
  teste. Se passar de ~150 KB, o subset desce (o eixo óptico é o primeiro
  candidato, porque é o mais caro: 30 KB).
- **`overrides` do markdownlint é configuração de ferramenta.** Se uma versão
  futura do `markdownlint-cli2` parar de aceitar `front_matter_title`, o job
  `docs` quebra alto, não silenciosamente. O caminho alternativo é um
  `.markdownlint-cli2.jsonc` no diretório (também medido, funciona), mas cria
  duas configurações para manter.

## O que fica para o builder

- Nomes de arquivo e componente, desde que a estrutura se mantenha.
- Estrutura interna do JSON derivado.
- CSS exato dentro dos tokens.
- Texto dos 54 overrides: `proposta`, `caminho`, `consulta`, `corpo`,
  `arquivos`, `verificar`, `relacionados`, os 37 `erros` que o contrato não
  resolve e as notas — revisado pela política editorial.

## Definição de pronto

- [ ] `npm run build` gera 69 HTML: 54 de operação, 13 de domínio, o índice da
      referência e a home.
- [ ] As sete falhas de build têm teste que prova a falha, não só o caminho
      feliz.
- [ ] A tabela de erros sai do contrato nos 115 pares deriváveis e do override
      nos 37 que ele não resolve; nenhuma das duas fontes é duplicada.
- [ ] Nenhum valor de cor/tipo/espaçamento fora de `tokens.css`.
- [ ] Um `h1` por página, skip link, `aria-current` na navegação.
- [ ] Nenhum HTML com `<script>` sem `src` (CSP `script-src 'self'`).
- [ ] `docs/api/portal.md` (seção de operação por página, `script` módulo,
      `try_files`, formato do override, seção 6 como esqueleto),
      `docs/api/inventario.md` (nomes de domínio = tags, seção 6),
      `docs/api/politica-editorial.md` (nota do esqueleto na F2.1),
      `docs/api/backlog.md`, `docs/ci-cd.md`, `docs/INDEX.md`,
      `.markdownlint-cli2.jsonc` (glob do `docs-site` + `overrides` de `MD041`),
      `Projetos/fisga/README.md` e `AGENTS.md` do Fisga (invariante de
      documentação) atualizados no mesmo PR.
- [ ] Job `docs-site` verde, `verificar-rotas.mjs` com 54/54/54,
      markdownlint 0.
