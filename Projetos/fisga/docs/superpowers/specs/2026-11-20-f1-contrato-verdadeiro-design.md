# Fisga — F1: contrato verdadeiro (design de execução)

**Data:** 20/11/2026
**Status:** em aprovação
**Issues:** #62–#71 (fase F1 do programa #58)
**Escopo:** tornar o `docs/api/openapi.json` suficiente para documentar a API sem
ler o código. Nenhuma mudança de comportamento, exceto o `Retry-After` do `429`
(F1.8), que é aditivo.

## Intenção

O contrato é gerado do código e o CI já regenera e compara, então ele nunca
"mente por esquecimento" — mente por omissão: 12 lacunas medidas no
[inventário](../../api/inventario.md). O portal da F2 será gerado deste arquivo;
cada lacuna vira uma página errada, vazia ou uma informação que o leitor só
descobre testando. A F1 fecha as lacunas antes de qualquer página existir.

## Objetivos

- Cada uma das 54 operações com tag de domínio, `operationId` estável, resumo e
  descrição em português.
- Segurança explícita por operação: `cookieAuth`, `adminToken` ou anônima.
- Status reais (`201`/`202`/`204`) e erros com um único schema.
- Rate limit e cache visíveis no contrato, gerados da metadata real.
- Uploads com schema `multipart/form-data`.
- `info` e `servers` completos.
- Uma guarda que impede spec, README e inventário de divergirem.

## Não-objetivos

- Não mudar comportamento da API (só o header `Retry-After` é novo).
- Não migrar `ErroResponse` para RFC 9457; o formato real é documentado como é.
- Não publicar o portal (F2), nem console (F3), nem sandbox (F4).
- Não reescrever os handlers; metadata entra onde o endpoint já é declarado.

## Restrições

- `openapi.json` é artefato: gerado por `scripts/gerar-openapi.sh`, comparado no
  job `api`. Nenhuma edição à mão.
- `gate` é o único check obrigatório; `docs` roda em PR de documentação.
- Mudança de contrato atualiza a documentação no mesmo PR.
- Tudo em português do Brasil: `summary`, `description`, tags e `operationId`.

## Abordagens consideradas

**A — metadata inline + mapa de `operationId` em código (escolhida).** Cada
endpoint declara `.ComTag(...)`, `.ComResumo(...)`, `.ComOperacao(...)` na
própria declaração; um único `MapaDeOperacoes` em `src/Fisga.Api/OpenApi/`
congela `(método, rota) → operationId`. O transformer preenche segurança,
rate limit e cache lendo a metadata que o endpoint já tem. Vantagem: quem lê o
endpoint vê o contrato; o mapa dá o diff explícito quando uma rota muda.
Desvantagem: dois lugares para uma operação nova — cobertos por teste.

**B — tabela central com toda a metadata.** Um arquivo com as 54 operações
(tag, id, resumo, segurança). Vantagem: contrato revisável de uma vez.
Desvantagem: separa a documentação do comportamento; renomear handler não obriga
a revisar a entrada; o endpoint fica anônimo no código.

**C — metadata por atributo nos handlers.** `[OpenApiOperation(...)]` nos
métodos. Desvantagem: os handlers são lambdas inline; atributo não cabe, e a
metadata ficaria longe da rota.

**D — documentar só no portal (prosa).** Descartada: repete o problema que a F0
resolveu; a página nasce desatualizada no primeiro endpoint novo.

## Decisões de desenho

### Tags (13)

| Tag | Operações |
| --- | --- |
| `Sistema` | `/health`, `/ready`, `/api/legal/versoes` |
| `Catálogo` | `/api/catalogo` |
| `Pescarias` | 6 operações de `/api/pescarias` |
| `Locais` | 7 operações de `/api/locais` |
| `Pontos` | 4 de `/api/pontos` |
| `Trilhas` | 4 de `/api/trilhas` |
| `Diário` | `/api/diario` |
| `Mídia` | `/api/midia` |
| `Espécies` | `/api/especies/sugerir` |
| `Condições` | `/api/condicoes`, `/api/condicoes/comparar` |
| `Autenticação` | 16 de `/api/auth/*` |
| `Conta` | 5 de `/api/conta/*` |
| `Newsletter` | 3 de `/api/newsletter/*` |

`Sistema` absorve `legal/versoes` para fechar em 13; a versão pública dos
documentos é infraestrutura, não domínio de conta.

### `operationId`

Convenção `verboRecurso` em camelCase, português, congelada em
`src/Fisga.Api/OpenApi/MapaDeOperacoes.cs`:

```text
confirmarLink          confirmarEmail         entrarComSenha
esquecerSenha          concluirLoginGoogle    conectarGoogle
concluirConexaoGoogle  googleDisponivel       iniciarLoginGoogle
obterSessao            atualizarPerfil        redefinirSenha
registrarConta         sair                   sairDeTodos
solicitarLink          obterCatalogo          obterCondicoes
compararCondicoes      excluirConta           registrarConsentimento
exportarConta          alterarSenha           listarSessoes
obterDiario            sugerirEspecies        versoesLegais
listarLocais           criarLocal             atualizarLocal
excluirLocal           definirLocalPadrao     buscarLocais
reverterCoordenadas    enviarMidia            descadastrarNewsletter
enviarNewsletter       criarPescaria           previsualizarNewsletter
atualizarPescaria      obterPescariaDoDono    obterPescariaPorToken
exportarPescariaPdf    obterPermissaoPescaria listarPontos
criarPonto             atualizarPonto         excluirPonto
listarTrilhas          criarTrilha            atualizarTrilha
excluirTrilha          obterSaude             obterProntidao
```

São exatamente 54 nomes, um por operação do spec. `GET /api/pescarias` não
existe — não há `listarPescarias`. `GET /api/conta` também não existe: o
consentimento é registrado por `POST /api/conta/consentimento`
(`registrarConsentimento`) e não tem operação de leitura própria.

O mapa é a fonte: um teste falha se o spec tiver operação sem entrada, entrada
sem operação ou `operationId` duplicado.

### Segurança derivada

Um `IOpenApiOperationTransformer`:

- endpoint com `IAllowAnonymous` → `security: []` (explícito, não ausente);
- endpoint com metadata `RequerTokenAdmin` → `[{adminToken: []}]`;
- qualquer outro → `[{cookieAuth: []}]`.

Schemes em `components.securitySchemes`: `cookieAuth` (`apiKey`, cookie
`fisga_auth`, descrição com `HttpOnly`, `SameSite=Lax`, `Secure` por
configuração e validade deslizante de 30 dias) e `adminToken` (`apiKey`, header
`X-Admin-Token`). `shareToken` **não** é scheme: é parâmetro de rota já tipado;
ganha `description` avisando que é credencial de leitura do recurso.

`GET /api/auth/me` e `POST /api/auth/sair` ficam anônimos por decisão — `/me`
responde `200` com `autenticado:false`; `sair` sem sessão é `204`.

### Rate limit e cache derivados

O endpoint continua chamando `.RequireRateLimiting("leitura")`; o transformer lê
`EnableRateLimitingAttribute.PolicyName` (metadata que o `RequireRateLimiting`
já grava) e o valor configurado em `RateLimiting:<política>`, e escreve:

```json
"x-rate-limit": { "policy": "leitura", "requests": 180, "windowSeconds": 60 }
```

Quatro operações não têm política e **não ganham uma neste pacote** (seria
mudança de comportamento): `/health`, `/ready`, `POST /api/newsletter/enviar` e
`GET /api/newsletter/preview` — as duas administrativas são protegidas por
`X-Admin-Token` e ficam sem limite por IP. O transformer não inventa valor para
elas e o teste 4 compara o mapa completo operação → política, não a presença
cega. Adicionar limite às rotas administrativas fica registrado como decisão
separada, fora da F1.

`x-cache` é a string exata do `Cache-Control` que a operação emite num 2xx
(inclui `stale-while-revalidate`, como em `/api/condicoes`), e existe em todas
as 54 operações: sem `CachePolicy` o default-deny emite `private, no-store`, e o
contrato diz isso em vez de omitir. Erro (>= 400) sempre sai `private, no-store`
por `DecisaoDeCache` — a descrição do endpoint registra essa exceção.

```json
"x-cache": "public, max-age=86400"
"x-cache": "private, max-age=300, stale-while-revalidate=600"
"x-cache": "private, no-store"
```

### Status e erros (pacote A2)

- `Produces<T>(201/202/204)` nos handlers que devolvem algo diferente de `200`.
  Os quatro `POST` de criação já mandam `Location`; o contrato passa a declarar.
- **Cada status declara a forma real, sem normalizar comportamento.** O contrato
  tem três formas de erro e diz qual é qual:
  - `ErroResponse` (`codigo`, `detalhe`) nos erros de negócio dos handlers;
  - `ProblemDetails` no `400` de binding do framework (`AddProblemDetails` já
    está ligado em `Program.cs`), com `description` explicando que validação de
    corpo cai aqui e não em `ErroResponse`;
  - **corpo vazio** onde hoje é vazio: `503` de `/ready`, `404` de
    `/api/locais/reverso` sem resultado, `404` proposital das duas rotas
    administrativas, `413` do Kestrel em upload e `429` do rate limiter. O
    status aparece sem `content`, com `description` dizendo por quê.

  Isso é o que a F1.3 pede: "o spec não mistura ProblemDetails do framework com
  `ErroResponse` sem dizer qual é qual". Migrar tudo para um schema só seria
  mudança de comportamento e está fora de escopo.
- `ProducesProblem` por status, apontando para o schema correto de cada um.
- `Retry-After` em segundos na rejeição do limiter.
- Multipart em `/api/midia` e `/api/especies/sugerir`: campo `arquivo`, tipos
  aceitos e limite de 5 MB.

### Documento e guardas (pacote A3)

- `info.contact`, `info.license` (proprietária) e `info.description`.
- `servers` **fixo e independente de ambiente**, com as duas entradas na ordem
  produção → local:

  ```json
  [
    { "url": "https://api.fisga.abson.tech", "description": "Produção" },
    { "url": "http://localhost:5080", "description": "Local" }
  ]
  ```

  O artefato é gerado com `ASPNETCORE_ENVIRONMENT=Development` e comparado byte
  a byte no CI; `servers` escolhido por ambiente faria o arquivo commitado
  carregar a URL local e o portal publicar um ambiente morto. Fixo, o JSON é
  estável nos dois sentidos.
- Comportamentos que surpreendem: `404` proposital do admin, `GET` de auth com
  `302`, `/me` com `200` e `autenticado:false`, `403 consentimento_pendente`,
  `403 origem_invalida`.
- Script no job `docs` comparando rotas de `openapi.json`, do resumo do
  `README.md` e do inventário.
- **Lint de OpenAPI no job `api`**, que é quem já vê mudança de contrato:
  `npx --yes @redocly/cli@2 lint docs/api/openapi.json --extends=minimal`. O
  ruleset `minimal` valida estrutura e referências sem impor estilo — não
  transforma a F1.7 numa reforma de 54 operações nem duplica os invariantes que
  o teste C# já cobre.

## Execução em três PRs

| Pacote | Issues | Entrega |
| --- | --- | --- |
| **A1 — identidade e segurança** | #62 (F1.4), #63 (F1.1), #67 (F1.6) | tags, `operationId`, resumos, schemes, `x-rate-limit`, `x-cache` |
| **A2 — respostas** | #64 (F1.2), #65 (F1.3), #66 (F1.5), #69 (F1.8) | status reais, schema de erro, multipart, `Retry-After` |
| **A3 — documento e guardas** | #68 (F1.7), #70 (F1.9), #71 (F1.10) | `info`, `servers`, comportamentos, script de divergência |

F1.6 entrou no A1 (não no A2 como na primeira conversa): é a mesma metadata por
operação, nas mesmas linhas de declaração — separar só criaria conflito.

Cada PR: `Closes` das suas issues, contrato regerado, testes de contrato verdes,
documentação atualizada (inventário, contagem de lacunas, `backlog.md`).

### Estratégia de documentação

As frases "13 lacunas"/"12 lacunas" espalhadas por `inventario.md`,
`backlog.md`, `docs/api/README.md` e ADR 0017 viram "as lacunas do contrato",
mantendo a contagem apenas no cabeçalho da tabela do inventário. Assim cada
pacote remove sua linha sem editar quatro arquivos por número.

### Testes

`tests/Fisga.Api.Tests/ContratoTests.cs`, sobre o fixture existente:

1. Toda operação tem `operationId` no mapa, sem duplicata, e resumo não vazio.
2. Tags restritas às 13; nenhuma `Fisga.Api`.
3. Anônimas com `security: []`; as demais com `cookieAuth`; admin com
   `adminToken`.
4. `x-rate-limit` igual ao mapa esperado operação → política, com as quatro
   operações sem política (`/health`, `/ready`, newsletter admin) explicitamente
   ausentes; o número bate com `RateLimiting:*`. `x-cache` igual a
   `CachePolicy.HeaderValue`, ou `private, no-store` quando não há policy —
   presente nas 54 operações.
5. Contagem de operações do spec igual à do inventário (54).
6. A2: os quatro `POST` de criação declaram `201` com `Location`; `429`, `503`
   de `/ready`, `404` de `/locais/reverso`, `404` do admin e `413` de upload
   declarados sem corpo; `400` de binding aponta para `ProblemDetails`; os
   demais erros apontam para `ErroResponse`.
7. A3: `info`, `servers` (produção e local) e os cinco comportamentos
   surpreendentes presentes; `@redocly/cli lint --extends=minimal` verde no job
   `api`.

O job `api` continua garantindo que o JSON commitado é o gerado.

## Decisões fechadas na revisão do design

Seis pontos que a primeira versão deixava em aberto; todos resolvidos sem mudar
o escopo:

1. **Formas de erro.** O contrato declara a forma real por status:
   `ErroResponse`, `ProblemDetails` (binding) ou corpo vazio (503 de `/ready`,
   404 de `/locais/reverso`, 404 do admin, 413 de upload, 429). Normalizar
   tudo seria mudança de comportamento.
2. **Rate limit ausente no admin.** As duas rotas de newsletter não ganham
   política nesta fase; o contrato registra a ausência e o teste compara o mapa
   completo. Adicionar limite lá é decisão separada.
3. **`servers` fixo** com produção e local, para o artefato commitado ser
   estável e o portal nunca apontar para ambiente morto.
4. **`x-cache` como string** igual ao `Cache-Control` do 2xx — cabe
   `stale-while-revalidate`, não exige estender `CachePolicy` e aparece nas 54
   operações (default-deny incluído).
5. **Lint de OpenAPI** entra no job `api` com `@redocly/cli --extends=minimal`,
   sem reforma de estilo.
6. **Mapa de `operationId`** corrigido para 54 nomes: sai `listarPescarias`
   (rota inexistente), entra `obterPermissaoPescaria`; `Locais` tem 7
   operações, não 8.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Renomear rota sem atualizar o mapa quebra o teste | É o objetivo: o erro aparece no CI, não no portal |
| `AllowAnonymous` não refletir a intenção real de alguma rota | Revisão operação a operação no inventário; o teste exige decisão explícita |
| `x-rate-limit` divergir da configuração | Valor lido da configuração no transformer + teste comparando |
| 54 declarações editadas gerarem erro mecânico | Teste cobre ausência/duplicata; `openapi.json` no diff mostra o resultado |

## O que fica para o builder

- Escolher nomes finais de tag/`operationId` que leiam bem em português, sem
  inventar vocabulário fora do glossário do produto.
- Decidir a forma do helper (`ComTag`/`ComOperacao` juntos ou um só `ComContrato`).
- Escrever os `summary`/`description` de negócio (uma frase de propósito + uma de
  comportamento), sem copiar código.

## F2.1 em paralelo

Depois do A1, o esqueleto do `docs-site` pode começar: o gerador precisa de
`operationId` estável para a URL das páginas e de tags para a navegação — os dois
saem do A1. O gerador roda contra o spec do momento e é revalidado no fim da F1;
o conteúdo da F2.2 entra depois do A3.
