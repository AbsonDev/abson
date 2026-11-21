# Fisga F2.1 — esqueleto do portal e gerador da referência: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publicar em `Projetos/fisga/docs-site/` o site Astro estático com 69 páginas (home, índice da referência, 13 domínios e 54 operações), geradas de `docs/api/openapi.json` mais um override obrigatório por operação, falhando o build quando contrato e override não bastam.

**Architecture:** Um módulo Node puro (`scripts/gerar-referencia.mjs`) lê o contrato e os 54 overrides, valida as sete falhas, deriva tudo que é mecânico (autenticação, limites, cache, tabela de erros, esqueleto de resposta, `curl`) e emite `src/data/referencia.json`. As páginas `.astro` só renderizam esse JSON — nenhuma resolve OpenAPI em tempo de build. O site é `output: 'static'`, `build.format: 'file'`, tokens copiados de `design-system/fisga/tokens.css` e duas fontes self-hosted.

**Tech Stack:** Astro 7.4.13 (estático), Node 24 no CI (Astro exige ≥22.23.4), `yaml` 2.9, `marked` 18, `@fontsource-variable/{fraunces,inter}` 5.3, `node --test`, GitHub Actions.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-21-f2-1-esqueleto-e-referencia-design.md`

## Global Constraints

- `docs/api/openapi.json` é entrada: o portal lê, nunca reescreve. Nenhuma mudança em `src/`, `tests/` ou no contrato neste plano.
- UI e copy em pt-BR; termos de API (`endpoint`, `cookie`, `header`, `payload`) em inglês com glosa na primeira aparição.
- Nenhum segredo, token real ou PII nos exemplos: e-mail `@example.com`, `SEU_TOKEN_AQUI`, `SEU_ADMIN_TOKEN`, `shareToken` inventado com formato plausível, coordenada de cidade.
- Nenhum valor literal de cor, tipografia ou espaçamento fora de `src/styles/gerado/tokens.css` (cópia de `design-system/fisga/tokens.css`). Tints só com `color-mix()` sobre token existente.
- `output: 'static'`, `trailingSlash: 'never'`, `build.format: 'file'`, `vite.build.assetsInlineLimit: 0`. `is:inline` é proibido; nenhum HTML pode sair com `<script>` sem `src` (CSP `script-src 'self'`).
- Uma operação por URL: `/referencia/<slug-do-dominio>/<operationId>`. Nada de `src/pages/referencia/index.astro`.
- Ordem canônica: `sistema · catalogo · autenticacao · conta · pescarias · diario · condicoes · locais · pontos · trilhas · midia · especies · newsletter`. Dentro do domínio, ordem de `paths` do contrato.
- Título de domínio = `name` da tag do contrato; slug = tag sem acento.
- `openapi.json` é a única fonte de rota, status, `codigo`, parâmetro e schema. O override só escreve o que o contrato cala.
- Commits convencionais pequenos, um por task. Rodar `npx --yes markdownlint-cli2@0.24.1` da raiz do repo antes de cada commit.

## Review Focus

1. **Request sem `required`** (`criarPescaria`, `atualizarPescaria`): o corpo do override tem que ser aceito pelo handler. Um exemplo com campo faltando vira `400` e a página ensina errado.
2. **`enum` com sentinela** (`NaoInformada`, `NaoInformado`, `NaoSeAplica`): o esqueleto derivado não pode escolher a sentinela como valor de exemplo.
3. **Desfecho `302` sem `Location`** (`confirmarLink`, Google, `descadastrarNewsletter`): a página não pode inventar URL nem prometer header que o contrato não declara.
4. **Query param nunca é obrigatório** (`buscarLocais`, `obterCondicoes`): a tabela não pode marcar `q`/`lat`/`lon` como obrigatórios, e o exemplo das três operações que devolvem `400` sem coordenada precisa declarar `consulta`.
5. **Resposta que não é JSON** (`text/html` em `previsualizarNewsletter`, `application/pdf` em `exportarPescariaPdf`): sem bloco de resposta, com o media type declarado.
6. **Lacuna declarada** (`enviarNewsletter`): a página não pode listar `mes`/`dryRun` como se estivessem no contrato. O override não os menciona e a lacuna vai para o inventário.

---

## Estrutura de arquivos

```text
Projetos/fisga/docs-site/
├── package.json                    # scripts: tokens, gerar, preparar, dev, build, check, test:unit, test:paginas
├── package-lock.json
├── astro.config.mjs
├── tsconfig.json
├── README.md                       # como rodar, como adicionar operação, home provisória
├── scripts/
│   ├── copiar-tokens.mjs           # design-system → src/styles/gerado (gitignorado)
│   ├── gerar-referencia.mjs        # contrato + overrides → src/data/referencia.json
│   ├── gerar-referencia.test.mjs   # unidade + integração com o contrato real
│   └── verificar-paginas.test.mjs  # pós-build: 69 HTML
└── src/
    ├── data/referencia.json        # gerado, gitignorado
    ├── styles/gerado/tokens.css    # copiado, gitignorado
    ├── styles/fontes.css
    ├── styles/portal.css
    ├── components/{Base,Nav,PreRequisitoDeSessao,BlocoDeCodigo,
    │               TabelaDeErros,SeloDeStatus,TabelaDeSchema}.astro
    └── pages/
        ├── index.astro
        ├── referencia.astro
        └── referencia/
            ├── [dominio].astro
            └── [dominio]/[operacao].astro
Projetos/fisga/docs/publico/operacoes/<operationId>.md   # 54 overrides
```

O `docs-site` não tem `.gitignore` próprio: a raiz já ignora `dist/`, `node_modules/` e `.astro/`. `src/data/referencia.json` e `src/styles/gerado/` entram no `.gitignore` da raiz, no Task 1.

### Task 1: Scaffold do site, tokens e fontes

**Files:**
- Create: `Projetos/fisga/docs-site/package.json`
- Create: `Projetos/fisga/docs-site/astro.config.mjs`
- Create: `Projetos/fisga/docs-site/tsconfig.json`
- Create: `Projetos/fisga/docs-site/README.md`
- Create: `Projetos/fisga/docs-site/scripts/copiar-tokens.mjs`
- Create: `Projetos/fisga/docs-site/src/styles/fontes.css`
- Create: `Projetos/fisga/docs-site/src/styles/portal.css`
- Create: `Projetos/fisga/docs-site/src/components/Base.astro`
- Create: `Projetos/fisga/docs-site/src/pages/index.astro`
- Modify: `.gitignore` (raiz)
- Modify: `.markdownlint-cli2.jsonc` (glob do README)

**Interfaces:**
- Produces: `Base.astro` com props `{ titulo: string, descricao: string, ativo?: string, versao?: string }`; `portal.css` importado uma vez; `npm run build` funcionando sem gerador e sem overrides.

- [ ] **Step 0: Commitar a spec e este plano**

```bash
git add Projetos/fisga/docs/superpowers/specs Projetos/fisga/docs/superpowers/plans
git commit -m "docs(portal): spec e plano da F2.1"
```

O restante do plano assume que esses dois arquivos já estão versionados; sem
isso o primeiro commit do site levaria junto um diff de documentação não
relacionado.

- [ ] **Step 1: Criar `package.json` com as versões e os scripts**

```json
{
  "name": "fisga-docs-site",
  "private": true,
  "type": "module",
  "scripts": {
    "tokens": "node scripts/copiar-tokens.mjs",
    "gerar": "node scripts/gerar-referencia.mjs",
    "preparar": "npm run tokens",
    "predev": "npm run preparar",
    "dev": "astro dev",
    "prebuild": "npm run preparar",
    "build": "astro build",
    "preview": "astro preview",
    "precheck": "npm run preparar",
    "check": "astro check",
    "pretest:unit": "npm run preparar",
    "test:unit": "node --test scripts/gerar-referencia.test.mjs",
    "test:paginas": "node --test scripts/verificar-paginas.test.mjs"
  },
  "dependencies": {
    "@fontsource-variable/fraunces": "^5.3.0",
    "@fontsource-variable/inter": "^5.3.0",
    "astro": "7.4.13",
    "marked": "^18.2.1",
    "yaml": "^2.9.1"
  },
  "devDependencies": {
    "@astrojs/check": "^0.9.12",
    "typescript": "^6.0.3"
  }
}
```

`typescript` fica em `^6` porque `@astrojs/check@0.9.12` declara peer `^5 || ^6` (medido).

- [ ] **Step 2: Criar `astro.config.mjs` e `tsconfig.json`**

`astro.config.mjs`:

```js
import { defineConfig } from 'astro/config';

export default defineConfig({
  site: 'https://docs.fisga.abson.tech',
  output: 'static',
  trailingSlash: 'never',
  build: { format: 'file' },
  devToolbar: { enabled: false },
  vite: { build: { assetsInlineLimit: 0 } }
});
```

`tsconfig.json`: `{ "extends": "astro/tsconfigs/strict", "include": [".astro/types.d.ts", "src/**/*", "scripts/**/*"] }`.

- [ ] **Step 3: Escrever `scripts/copiar-tokens.mjs`**

Lê `../../design-system/fisga/tokens.css` (relativo a `docs-site/scripts/`, via `new URL`), cria `src/styles/gerado/` com `mkdirSync(..., { recursive: true })` e copia byte a byte. Se o arquivo de origem não existir, sai com código 1 e a mensagem `tokens.css não encontrado em design-system/fisga/`. Não escreve nada além de `src/styles/gerado/tokens.css`.

- [ ] **Step 4: Escrever `src/styles/fontes.css` e `src/styles/portal.css`**

`fontes.css` declara exatamente dois `@font-face`, apontando para os arquivos do pacote (import de CSS do pacote emite as 10 variantes de subset, medido):

```css
@font-face {
  font-family: 'Fraunces';
  font-style: normal;
  font-weight: 100 900;
  font-display: swap;
  src: url('@fontsource-variable/fraunces/files/fraunces-latin-standard-normal.woff2') format('woff2-variations');
}
@font-face {
  font-family: 'Inter';
  font-style: normal;
  font-weight: 100 900;
  font-display: swap;
  src: url('@fontsource-variable/inter/files/inter-latin-wght-normal.woff2') format('woff2-variations');
}
```

`portal.css` começa com `@import './gerado/tokens.css';` e `@import './fontes.css';` e define: `html`/`body` com `--bg`/`--fg`, `--font-body`, `--text-base`, `--leading-body`, `font-optical-sizing: auto`; `.container` com `max-width: var(--container-max)`, gutters `--container-gutter-phone`; tipografia de `h1`–`h3` com `--font-display`, `--tracking-display`, `--leading-tight`; `.skip-link` visível no foco; `a` em `--accent`; `:focus-visible` com `--focus-ring`; tabelas com `--border` e `--surface-warm` no `thead`; `pre`/`code` com `--font-mono` e `--surface`; `.chip` com `--radius-sm`; grid de referência (aside + artigo) colapsando em uma coluna abaixo de 900px; `@media (prefers-reduced-motion: reduce)` sem transição.

- [ ] **Step 5: Escrever `Base.astro` e `index.astro` provisórios**

`Base.astro` recebe `titulo`, `descricao`, `ativo`, `versao?`; emite `<!doctype html>`, `lang="pt-BR"`, `<meta name="description">`, `<link rel="canonical">`, importa `portal.css`, skip link para `#conteudo`, `<header>` com link para `/` e para `/referencia` (com `aria-current="page"` quando `ativo === 'referencia'`), `<main id="conteudo">` com `<slot />` e `<footer>` com a versão do contrato **quando `versao` for passada** e link para a API. `index.astro` usa `Base` sem `versao` e um `<h1>` provisório "Documentação da API do Fisga" — substituído no Task 6, que passa `info.versao`.

O canonical **não** pode sair de `Astro.url.pathname` cru: com `build.format: 'file'` o pathname é `/index.html`, `/referencia.html`, `/referencia/conta.html` (medido no Astro 7.4.13), então cada página apontaria para a própria variante `.html`. Normalizar no `Base`:

```js
const caminho = Astro.url.pathname
  .replace(/\/index\.html$/, '/')
  .replace(/\.html$/, '');
const canonical = new URL(caminho, Astro.site).href;
```

Medido com essa normalização: `https://docs.fisga.abson.tech/`, `/referencia`, `/referencia/conta`, `/referencia/conta/listarSessoes` — sem `.html` e sem barra final, casando com `trailingSlash: 'never'`.

- [ ] **Step 6: Ajustar `.gitignore` e o markdownlint; criar o README**

`.gitignore` (raiz) ganha:

```gitignore
# docs-site (gerado no build)
Projetos/fisga/docs-site/src/data/
Projetos/fisga/docs-site/src/styles/gerado/
```

`.markdownlint-cli2.jsonc`: adicionar `"Projetos/fisga/docs-site/README.md"` ao array
`globs` e o `overrides` de `MD041` que os overrides de operação exigem desde o
primeiro arquivo (medido: sem ele, um `.md` com `proposta` no frontmatter e
qualquer linha de prosa depois do `---` falha em `MD041`):

```jsonc
  "overrides": [
    {
      // O frontmatter do override começa com `proposta`, que é o "título"
      // semântico do arquivo; sem isso o MD041 acusa a primeira linha do corpo.
      "filter": ["Projetos/fisga/docs/publico/operacoes/**/*.md"],
      "config": { "MD041": { "front_matter_title": "^\\s*proposta\\s*[:=]" } },
      "combine": "merge"
    }
  ]
```

Conferir: `npx --yes markdownlint-cli2@0.24.1` continua com 0 issues (o
`filter` não casa nenhum arquivo ainda, porque `docs/publico/operacoes/` está
vazio).

Nota sobre o `astro check`: ele lista os `.mjs` do `include`, mas só os
type-checka com `checkJs` ligado — e ligar `checkJs` num gerador JS puro
transformaria todo `any` implícito em erro. O `astro check` guarda `.astro` e
`.ts`; a correção do gerador é responsabilidade do `node --test`. Não é
redundância, é divisão de trabalho.

`README.md` do site: o que é, comandos (`npm ci`, `npm run dev`, `npm run build`, `npm run test:unit`, `npm run test:paginas`), aviso de que a home é provisória na F2.1, e como adicionar operação (criar `docs/publico/operacoes/<operationId>.md`, rodar `npm run gerar`).

- [ ] **Step 7: Instalar, buildar e verificar o resultado**

Run:
```bash
cd Projetos/fisga/docs-site && npm install && npm run build && \
  find dist -type f | sort && \
  grep -o 'url([^)]*\.woff2)' dist/_astro/*.css | sort -u | wc -l
```
Expected: `dist/index.html` e um `.css` em `dist/_astro/`; a contagem de URLs
`.woff2` distintas é **2** (Fraunces ~67 KB, Inter ~48 KB) e
`find dist -name '*.woff2' | wc -l` também é `2`. `grep -o '<script[^>]*>'
dist/index.html` não devolve nada. `npm run check` termina com 0 erros.

- [ ] **Step 8: Commit**

```bash
git add .gitignore .markdownlint-cli2.jsonc Projetos/fisga/docs-site
git commit -m "feat(docs-site): esqueleto Astro do portal com tokens e fontes"
```

O `package-lock.json` entra neste commit — `npm install` gera, `npm ci` do CI
consome. Sem ele o job `docs-site` falha no primeiro PR que tocar o portal.

### Task 2: Núcleo do gerador — contrato em modelo de página

**Files:**
- Create: `Projetos/fisga/docs-site/scripts/gerar-referencia.mjs`
- Create: `Projetos/fisga/docs-site/scripts/gerar-referencia.test.mjs`

**Interfaces:**
- Produces (exports do módulo):
  - `DOMINIOS: ReadonlyArray<{ slug: string, tag: string }>` na ordem canônica.
  - `slugDoDominio(tag: string): string` — NFD, remove diacríticos, minúsculas.
  - `lerContrato(caminho?: URL): object`
  - `listarOperacoes(contrato): Array<{ operationId, metodo, rota, tag, operacao }>` na ordem de `paths`.
  - `derivarErros(operacao): Array<{ status, codigos: string[] | null, quando: string, origem: 'contrato' }>`
  - `auditar(contrato): { derivados: number, pendencias: Array<{ operationId, status }> }`
  - `derivarConsentimento(contrato): Set<string>`
  - `gerarModelo(contrato, overrides: Map<string, { arquivo: string, frontmatter: object, corpo: string }>): { info, dominios, operacoes, contagem }` — `contagem.paginas` = `2 + dominios.length + Object.keys(operacoes).length`.
  - `ErroDeGeracao` (classe) com `problemas: string[]`.

- [ ] **Step 1: Escrever os testes do núcleo (falhando)**

`scripts/gerar-referencia.test.mjs`, com `node:test` + `node:assert/strict`, importa do módulo. Fixture inline com as 13 tags do contrato e nove rotas, para exercitar `security`, `x-rate-limit`, `x-cache`, path/query param, `requestBody` JSON e multipart, `Location` e `302`:

```js
const TAGS = ['Sistema', 'Catálogo', 'Autenticação', 'Conta', 'Pescarias', 'Diário',
  'Condições', 'Locais', 'Pontos', 'Trilhas', 'Mídia', 'Espécies', 'Newsletter'];

const CONTRATO = {
  info: { title: 'API do Fisga', description: 'Descrição.', version: '1.0.0' },
  servers: [{ url: 'https://api.fisga.exemplo.tech', description: 'Produção' }],
  tags: TAGS.map((name) => ({ name, description: `Descrição de ${name}.` })),
  paths: {
    '/health': {
      get: {
        operationId: 'obterSaude', summary: 'Saúde', description: 'Diz se está no ar.',
        tags: ['Sistema'], security: [], 'x-cache': 'private, no-store',
        responses: { '200': { description: 'OK' } }
      }
    },
    '/api/conta': {
      delete: {
        operationId: 'excluirConta', summary: 'Exclui a conta', description: 'Exclui.',
        tags: ['Conta'], security: [{ cookieAuth: [] }],
        'x-rate-limit': { policy: 'escrita', requests: 40, windowSeconds: 60 },
        'x-cache': 'private, no-store',
        requestBody: {
          required: true,
          content: { 'application/json': { schema: { $ref: '#/components/schemas/ExcluirContaRequest' } } }
        },
        responses: {
          '204': { description: 'Sem corpo' },
          '400': { description: 'Corpo inválido.', content: { 'application/json': {} } },
          '401': { description: 'Sessão ausente (`nao_autenticado`).' },
          '403': { description: 'Sem permissão (`acesso_negado`) ou consentimento pendente (`consentimento_pendente`).' },
          '429': { description: 'Muitas requisições. Corpo vazio; o header `Retry-After` informa os segundos.' }
        }
      }
    },
    '/api/locais/{id}': {
      delete: {
        operationId: 'excluirLocal', summary: 'Exclui o local', description: 'Exclui.',
        tags: ['Locais'], security: [{ cookieAuth: [] }],
        parameters: [
          { name: 'id', in: 'path', required: true, schema: { type: 'string', format: 'uuid' } },
          { name: 'motivo', in: 'query', schema: { type: 'string' } }
        ],
        responses: {
          '204': { description: 'Sem corpo' },
          '404': { description: 'Recurso não encontrado. Corpo vazio.' }
        }
      }
    },
    '/api/condicoes': {
      get: {
        operationId: 'obterCondicoes', summary: 'Condições', description: 'Clima e maré.',
        tags: ['Condições'], security: [], 'x-cache': 'public, max-age=300',
        parameters: [
          { name: 'lat', in: 'query', schema: { type: ['number', 'string'], format: 'double' } },
          { name: 'lon', in: 'query', schema: { type: ['number', 'string'], format: 'double' } }
        ],
        responses: {
          '200': { description: 'OK', content: { 'application/json': { schema: { $ref: '#/components/schemas/Condicoes' } } } },
          '400': { description: 'Requisição inválida.', content: { 'application/json': {} } }
        }
      }
    },
    '/api/newsletter/enviar': {
      post: {
        operationId: 'enviarNewsletter', summary: 'Envia a newsletter', description: 'Dispara o envio.',
        tags: ['Newsletter'], security: [{ adminToken: [] }], 'x-cache': 'private, no-store',
        responses: {
          '200': { description: 'OK' },
          '404': { description: 'Recurso não encontrado. Corpo vazio.' },
          '500': { description: 'Erro inesperado (`codigo`: `erro_inesperado`).' }
        }
      }
    },
    '/api/pescarias': {
      post: {
        operationId: 'criarPescaria', summary: 'Cria a pescaria', description: 'Cria.',
        tags: ['Pescarias'], security: [{ cookieAuth: [] }], 'x-cache': 'private, no-store',
        requestBody: {
          required: true,
          content: { 'application/json': { schema: { $ref: '#/components/schemas/EntradaDePescaria' } } }
        },
        responses: {
          '201': { description: 'Criado', headers: { Location: { schema: { type: 'string' } } } },
          '400': { description: 'Corpo inválido.', content: { 'application/json': {} } }
        }
      }
    },
    '/api/conta/sessoes': {
      get: {
        operationId: 'listarSessoes', summary: 'Lista as sessões', description: 'Lista.',
        tags: ['Conta'], security: [{ cookieAuth: [] }], 'x-cache': 'private, no-store',
        responses: { '200': { description: 'OK' } }
      }
    },
    '/api/locais/buscar': {
      get: {
        operationId: 'buscarLocais', summary: 'Busca locais', description: 'Busca.',
        tags: ['Locais'], security: [], 'x-cache': 'public, max-age=3600',
        parameters: [{ name: 'q', in: 'query', schema: { type: 'string' } }],
        responses: { '200': { description: 'OK' } }
      }
    },
    '/api/midia': {
      post: {
        operationId: 'enviarMidia', summary: 'Envia a foto', description: 'Envia.',
        tags: ['Mídia'], security: [{ cookieAuth: [] }], 'x-cache': 'private, no-store',
        requestBody: {
          required: true,
          content: { 'multipart/form-data': { schema: { $ref: '#/components/schemas/FormularioDeFoto' } } }
        },
        responses: {
          '200': { description: 'OK' },
          '400': { description: 'Arquivo inválido.', content: { 'application/json': {} } }
        }
      }
    }
  },
  components: {
    schemas: {
      Condicoes: { type: 'object', required: ['agora'], properties: { agora: { type: 'string', format: 'date-time' }, tempo: { type: ['null', 'string'] } } },
      ExcluirContaRequest: { type: 'object', required: ['confirmacao'], properties: { confirmacao: { type: ['null', 'string'] } } },
      EntradaDePescaria: {
        type: 'object',
        properties: {
          local: { type: ['null', 'string'] },
          ambiente: { $ref: '#/components/schemas/AmbienteDePesca' },
          data: { type: 'string', format: 'date' },
          periodo: { $ref: '#/components/schemas/PeriodoDoDia' }
        }
      },
      AmbienteDePesca: { type: 'string', enum: ['Rio', 'Mar', 'Lagoa', 'Represa'] },
      PeriodoDoDia: { type: 'string', enum: ['Madrugada', 'Manha', 'Tarde', 'Noite'] },
      FormularioDeFoto: { type: 'object', required: ['arquivo'], properties: { arquivo: { type: 'string', format: 'binary' } } }
    },
    securitySchemes: {
      cookieAuth: { type: 'apiKey', in: 'cookie', name: 'fisga_auth' },
      adminToken: { type: 'apiKey', in: 'header', name: 'X-Admin-Token' }
    }
  }
};

// Um override por operação da fixture. Aqui o conteúdo ainda não é validado;
// do Task 3 em diante ele passa por `validarOverride`.
const OVERRIDES = new Map([
  ['obterSaude', { arquivo: 'obterSaude.md', frontmatter: { operacao: 'obterSaude', proposta: 'Diz se está no ar.', acesso: 'publico', verificar: 'Veja `status`.' }, corpo: '' }],
  ['excluirConta', { arquivo: 'excluirConta.md', frontmatter: { operacao: 'excluirConta', proposta: 'Exclui a conta.', acesso: 'sessao', verificar: 'Veja o `204`.', corpo: '{"confirmacao":"EXCLUIR"}', erros: [{ status: 400, codigos: ['confirmacao_invalida'], quando: 'A confirmação não é `EXCLUIR`.' }] }, corpo: '' }],
  ['excluirLocal', { arquivo: 'excluirLocal.md', frontmatter: { operacao: 'excluirLocal', proposta: 'Exclui o local.', acesso: 'dono', verificar: 'Veja o `204`.', caminho: { id: '00000000-0000-4000-8000-000000000001' } }, corpo: '' }],
  ['obterCondicoes', { arquivo: 'obterCondicoes.md', frontmatter: { operacao: 'obterCondicoes', proposta: 'Devolve clima e maré.', acesso: 'publico', verificar: 'Veja `tempo`.', consulta: { lat: -23.55, lon: -46.63 }, erros: [{ status: 400, codigos: ['local_sem_coordenadas'], quando: 'Faltam `lat`/`lon`.' }] }, corpo: '' }],
  ['enviarNewsletter', { arquivo: 'enviarNewsletter.md', frontmatter: { operacao: 'enviarNewsletter', proposta: 'Dispara o envio.', acesso: 'admin', verificar: 'Veja o total de enviados.' }, corpo: '' }],
  ['criarPescaria', { arquivo: 'criarPescaria.md', frontmatter: { operacao: 'criarPescaria', proposta: 'Cria a pescaria.', acesso: 'sessao', verificar: 'Veja o header `Location`.', corpo: '{"local":"Represa de Guarapiranga","ambiente":"Represa","data":"2026-11-21","periodo":"Manha"}', erros: [{ status: 400, codigos: ['data_invalida'], quando: 'A data é inválida.' }] }, corpo: '' }],
  ['listarSessoes', { arquivo: 'listarSessoes.md', frontmatter: { operacao: 'listarSessoes', proposta: 'Lista as sessões.', acesso: 'sessao', verificar: 'Veja a lista.' }, corpo: '' }],
  ['buscarLocais', { arquivo: 'buscarLocais.md', frontmatter: { operacao: 'buscarLocais', proposta: 'Busca locais.', acesso: 'publico', verificar: 'Veja a lista.', consulta: { q: 'Guarapiranga' } }, corpo: '' }],
  ['enviarMidia', { arquivo: 'enviarMidia.md', frontmatter: { operacao: 'enviarMidia', proposta: 'Envia a foto.', acesso: 'sessao', verificar: 'Veja a URL.', arquivos: { arquivo: 'foto.jpg' }, erros: [{ status: 400, codigos: ['midia_grande'], quando: 'A foto passa de 5 MB.' }] }, corpo: '' }]
]);
```

Casos, um `test` por linha:

- `test('slug sem acento e minúsculo', ...)`: `slugDoDominio('Autenticação') === 'autenticacao'`, `slugDoDominio('Espécies') === 'especies'`, `slugDoDominio('Condições') === 'condicoes'`.
- `test('ordem canônica cobre as 13 tags do contrato real', ...)`: `DOMINIOS.map(d => d.slug)` igual à lista do Global Constraints; com `lerContrato()`, o conjunto de `tags` é exatamente o conjunto de `DOMINIOS.tag`.
- `test('operação derivada traz método, rota e domínio', ...)`: `gerarModelo(CONTRATO, OVERRIDES).operacoes.obterSaude` tem `metodo === 'GET'`, `rota === '/health'`, `dominio === 'sistema'`.
- `test('autenticação vem de security', ...)`: `obterSaude.autenticacao === 'nenhuma'`; `excluirConta.autenticacao === 'sessao'`.
- `test('consentimento é cookieAuth com 403 de consentimento', ...)`: `derivarConsentimento(CONTRATO)` contém `excluirConta`; no contrato real tem tamanho 28 e não contém `registrarConsentimento`, `obterSessao` nem `sair`. Cada operação do modelo carrega `consentimento: true|false` e, quando verdadeiro, `consentimentoTexto` não vazio.
- `test('erros derivados: codigo nomeado e corpo vazio; 400 com corpo fica pendente', ...)`: para `excluirConta`, `derivarErros` devolve `401` com `codigos: ['nao_autenticado']`, `403` com `['acesso_negado','consentimento_pendente']` e `429` com `codigos: null`; o `400` dessa operação **não** aparece, porque tem corpo e a descrição não nomeia código.
- `test('no contrato real, 115 pares derivam e 37 ficam pendentes', ...)`: com `lerContrato()`, `auditar` devolve `derivados === 115` e `pendencias.length === 37`; as pendências são `23× 400`, `12× 404`, `2× 503` e ficam em 33 operações (29 com uma, 4 com duas: `atualizarPescaria`, `atualizarLocal`, `atualizarPonto`, `atualizarTrilha`).
- `test('limites e cache vêm do contrato; ausência vira null', ...)`: `excluirConta.limites` tem `policy === 'escrita'`, `requests === 40`, `windowSeconds === 60`; `obterSaude.limites === null`; `obterSaude.xCache === 'private, no-store'`. Comparar campo a campo (o modelo pode carregar campos extras sem quebrar o teste).
- `test('parâmetros preservam local e obrigatoriedade', ...)`: `excluirLocal.parametros` tem, na ordem de `parameters`, o item com `nome === 'id'`, `local === 'path'`, `obrigatorio === true` e o item com `nome === 'motivo'`, `local === 'query'`, `obrigatorio === false`; `obterCondicoes.parametros` traz `lat`/`lon` com `obrigatorio === false`.
- `test('tag fora de DOMINIOS falha', ...)`: `{ ...CONTRATO, tags: [...CONTRATO.tags, { name: 'Inventada', description: '' }] }` lança `ErroDeGeracao` nomeando `Inventada`.
- `test('domínio da ordem canônica sem tag no contrato falha', ...)`: `tags: CONTRATO.tags.filter(t => t.name !== 'Conta')` lança nomeando `Conta`.
- `test('operação sem override falha com o operationId', ...)`: `gerarModelo(CONTRATO, new Map())` lança com os nove `operationId` da fixture na lista.
- `test('override de operação inexistente falha', ...)`: `new Map([...OVERRIDES, ['naoExiste', { arquivo: 'naoExiste.md', frontmatter: {}, corpo: '' }]])` lança com `naoExiste`.
- `test('contagem é calculada, não literal', ...)`: `gerarModelo(CONTRATO, OVERRIDES).contagem` é `{ operacoes: 9, dominios: 13, paginas: 24 }` (2 + 13 + 9).

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd Projetos/fisga/docs-site && npm run test:unit`
Expected: FAIL com `Cannot find module .../gerar-referencia.mjs`.

- [ ] **Step 3: Implementar o núcleo em `scripts/gerar-referencia.mjs`**

Caminhos: `const RAIZ = new URL('../', import.meta.url)`; contrato em `new URL('../docs/api/openapi.json', RAIZ)`; overrides em `new URL('../docs/publico/operacoes/', RAIZ)`.

Regras que o código precisa materializar:

- `DOMINIOS` na ordem do Global Constraints; comentário apontando `docs/api/portal.md#mapa-de-url`.
- `listarOperacoes`: percorre `contrato.paths` em ordem de inserção e os métodos `get/post/put/patch/delete`.
- `autenticacao`: `security` vazio → `'nenhuma'`; `[{cookieAuth: []}]` → `'sessao'`; `[{adminToken: []}]` → `'admin'`; qualquer outra forma → pendência `security não reconhecido`. No modelo, `autenticacaoTexto` é a forma legível: `'nenhuma'` → "sem autenticação", `'sessao'` → "cookie de sessão `fisga_auth`", `'admin'` → "header `X-Admin-Token`". A página mostra "Autenticação: <autenticacaoTexto>".
- `consentimento`: `derivarConsentimento` = operações com `cookieAuth` cujo `responses['403'].description` contém `consentimento_pendente`. O conjunto não é constante: sai do contrato.
- `derivarErros`: para cada status `>= 400`, na ordem numérica; `codigos` = todos os matches de `` /`([a-z][a-z0-9]*(?:_[a-z0-9]+)+)`/g `` na `description`; se não houver match e a descrição contiver `Corpo vazio`, `codigos: null`; se não houver match nem `Corpo vazio`, o par fica pendente (não entra em `derivarErros`).
- `auditar(contrato)` → `{ derivados, pendencias: Array<{ operationId, status }> }`; `pendencias` é a lista que o override precisa cobrir.
- `gerarModelo`: valida cobertura de tags nos dois sentidos (`DOMINIOS` × `contrato.tags`), exige um override por operação, rejeita `operationId` desconhecido e duplicado, e devolve `contagem` calculada — nunca literal.
- `info` no modelo: `{ titulo, descricao, versao, servidor, servidores }` — `servidores` é `contrato.servers.map(({ url, description }) => ({ url, descricao: description }))` e `servidor` é `servidores[0].url`. A lista inteira importa: o `curl` aponta para produção (o primeiro), e a página precisa oferecer a base local sem inventar URL.
- `dominios` no modelo: um item por `DOMINIOS`, na ordem canônica, com `{ slug, tag, titulo, descricao, total, operacoes: string[] }` — `titulo`/`descricao` vêm da tag do contrato, `total` é `operacoes.length`, e `operacoes` são os `operationId` do domínio na ordem de `paths`.
- `operacoes` no modelo: objeto indexado por `operationId`, na ordem de `paths`, para o import estático das páginas.
- Toda mensagem de pendência segue `"<arquivo ou operação>: <campo>: <motivo>"`; `ErroDeGeracao` agrega todas e `main()` imprime uma por linha e sai com 1.
- `main()` só escreve `src/data/referencia.json` quando não há pendência, com `JSON.stringify(modelo, null, 2)`.
- `main()` roda **apenas** sob a guarda `chamadoDiretamente()`, copiada de `scripts/verificar-rotas.mjs` (compara `pathToFileURL(process.argv[1]).href` e, como fallback, `realpathSync` — `/var` → `/private/var` no macOS). Sem a guarda, o import do módulo pelo teste executaria `main()` e o teste falharia com as pendências de `erros` que ainda não têm override.

- [ ] **Step 4: Rodar e ver passar**

Run: `cd Projetos/fisga/docs-site && npm run test:unit`
Expected: PASS nos casos do Step 1. Os de contrato real rodam de verdade: `lerContrato()` sem argumento aponta para o `openapi.json` da árvore.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/docs-site/scripts
git commit -m "feat(docs-site): gerador deriva operações, acesso, limites e erros do contrato"
```

### Task 3: Override — frontmatter, erros humanos e exemplo executável

**Files:**
- Modify: `Projetos/fisga/docs-site/scripts/gerar-referencia.mjs`
- Modify: `Projetos/fisga/docs-site/scripts/gerar-referencia.test.mjs`

**Interfaces:**
- Consumes: `auditar`, `gerarModelo`, `derivarErros` do Task 2.
- Produces:
  - `lerOverrides(diretorio?: URL): Array<{ arquivo: string, operacao: string, frontmatter: object, corpo: string }>` — é a única camada que vê todos os arquivos, então é ela que recusa `operacao` repetida e nome de arquivo diferente de `<operacao>.md`.
  - `validarOverride(entrada: { arquivo, frontmatter, corpo }, operacao: object, contrato: object): string[]` — devolve lista de problemas (vazia = válido). `operacao` é o item de `listarOperacoes`.
  - `montarCurl(operacao, override, servidor): string[]` — linhas do `curl`.
  - Cada `operacoes[operationId]` ganha `proposta`, `acesso`, `acessoTexto`, `consentimentoTexto`, `erros` (derivados + override, ordenados por status), `exemplo`, `verificar`, `notas` (Markdown cru do corpo) e `notasHtml` (o mesmo corpo já renderizado por `marked`), `relacionados: Array<{ operationId, titulo, dominio }>`, `parametros[].valor`, `corpo`/`arquivos`.

- [ ] **Step 1: Escrever os testes do override (falhando)**

Casos:

- `test('frontmatter com chave desconhecida falha', ...)`: `resposta: x` no override → problema `chave desconhecida: resposta`.
- `test('corpo com heading falha', ...)`: `corpo: '# Título'` → problema com `heading`.
- `test('status derivável no override falha', ...)`: `erros: [{ status: 401, codigos: [x] }]` num par derivável → problema `linha derivável do contrato`.
- `test('status >= 400 pendente sem linha falha', ...)`: `excluirConta` sem linha para `400` → problema `400`.
- `test('status que o contrato não declara falha', ...)`: `erros: [{ status: 418, ... }]` → problema `418`.
- `test('JSON sem corpo falha e multipart sem arquivos falha', ...)`: dois casos, mensagens distintas.
- `test('EntradaDePescaria sem required aceita corpo parcial, mas o exemplo ainda precisa ser objeto válido', ...)`: `criarPescaria` com `corpo: '{"ambiente":"Represa"}'` passa na validação (o schema não declara `required`); com `corpo: '[]'` falha com `corpo precisa ser objeto`.
- `test('consulta obrigatória de obterCondicoes é exigida pelo override, não pelo contrato', ...)`: `obterCondicoes` sem `consulta` → problema `consulta`; os `parameters` do contrato continuam com `required: false` em `lat`/`lon`.
- `test('corpo presente sem requestBody falha', ...)`: `obterSaude` com `corpo` → problema.
- `test('caminho obrigatório quando a rota tem path param', ...)`: `excluirLocal` sem `caminho` → problema; com `caminho: { outro: x }` → `chave fora de parameters`.
- `test('consulta com chave fora de parameters falha', ...)`: `buscarLocais` com `consulta: { termo: x }` → problema.
- `test('acesso incompatível com security falha', ...)`: `obterSaude` com `acesso: sessao` → problema; `listarSessoes` com `acesso: publico` → problema; `capacidade` numa rota sem token → problema.
- `test('relacionado inexistente falha', ...)`: `relacionados: [naoExiste]` → problema.
- `test('curl de sessão usa cookies.txt', ...)`: `montarCurl` de `listarSessoes` contém `-b cookies.txt` e não contém `X-Admin-Token`.
- `test('curl administrativo usa X-Admin-Token', ...)`: `enviarNewsletter` contém `-H "X-Admin-Token: SEU_ADMIN_TOKEN"`.
- `test('curl com path e query concretos', ...)`: `obterCondicoes` com `consulta` usa `?lat=-23.55&lon=-46.63` (ordem de `parameters`); `excluirLocal` usa o UUID do `caminho`.
- `test('a origem da linha é decidida só pela description, não por content', ...)`: tabela de casos `[nomeiaCodigo, dizCorpoVazio]` → derivado com código / derivado com `codigos: null` (vazios) / pendência. O `content` declarado não muda a classificação — por isso os três `404` de `enviarNewsletter`, `previsualizarNewsletter` e `reverterCoordenadas` derivam, e os outros `404` de recurso ficam pendentes.
- `test('3XX nunca entra em erros derivados', ...)`: `derivarErros` de uma operação sintética com `responses: { '302': {...}, '429': { description: 'Corpo vazio.' } }` devolve só o `429`; o `302` não aparece. (O caso do `confirmarLink` real é habilitado no Task 5, quando os 54 overrides existem.)

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd Projetos/fisga/docs-site && npm run test:unit`
Expected: FAIL nos casos novos (funções não exportadas).

- [ ] **Step 3: Implementar leitura e validação do override**

Regras exatas:

- `lerOverrides` lê `*.md` do diretório, extrai o frontmatter entre as duas primeiras linhas `---` com `parse` do `yaml` e guarda o resto como `corpo` (trim). Arquivo sem frontmatter → problema.
- Chaves aceitas: `operacao`, `proposta`, `acesso`, `caminho`, `consulta`, `corpo`, `arquivos`, `verificar`, `relacionados`, `erros`. Qualquer outra → problema (`chave desconhecida: <chave>`). `operacao`, `proposta`, `acesso`, `verificar` são obrigatórios e não vazios; `operacao` precisa ser igual ao nome do arquivo sem `.md`.
- `acesso` compatível: `publico`/`capacidade` → `security` vazio; `sessao`/`dono` → `cookieAuth`; `admin` → `adminToken`. `capacidade` exige `shareToken` em `parameters` (`in: path` ou `in: query`) ou `token` em query. `acessoTexto` é constante por valor, derivada só de `acesso`:

  | `acesso` | `acessoTexto` |
  | --- | --- |
  | `publico` | qualquer pessoa, sem autenticação |
  | `capacidade` | quem tem o link ou o token de compartilhamento |
  | `sessao` | qualquer pessoa com sessão iniciada |
  | `dono` | só o dono do recurso |
  | `admin` | só o operador com o token administrativo |

  `consentimentoTexto`, quando `consentimento` é verdadeiro, é a frase fixa: "Exige consentimento vigente. Sem ele a resposta é `403` com o `codigo` `consentimento_pendente`."

- `erros[]`: objeto com `status` (inteiro), `codigos` (lista de strings, não vazia) ou `codigo` (string, atalho), e `quando` (string não vazia). Status precisa existir nas respostas `>= 400` da operação **e** estar entre as pendências (`auditar`); caso contrário, `linha derivável do contrato` ou `status não declarado`. Dois itens com o mesmo `status` → problema (`status repetido`).
- `corpo`: string JSON válida, objeto; precisa ter todas as propriedades `required` do schema do `requestBody` (quando houver `required`); proibido sem `requestBody` JSON e obrigatório com ele.
- `arquivos`: objeto não vazio mapeando campo do schema multipart → nome de arquivo; obrigatório com `multipart/form-data`, proibido sem.
- `caminho`/`consulta`: toda chave precisa existir em `parameters` com o `in` certo; toda chave `caminho` exigida pela rota precisa estar presente; `consulta` proibida quando não há query param; `caminho` proibido quando não há path param. Valor de parâmetro com `format: 'uuid'` precisa casar com `/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i` — UUID de exemplo já é versão 4.
- Corpo do arquivo: nenhuma linha fora de bloco de código pode começar com `#` (detecção de fence ``` ```); o corpo é renderizado depois com `marked` no modelo (`notasHtml`).
- `relacionados`: cada `operationId` precisa existir no contrato; vira `{ operationId, titulo, dominio }`, com `titulo` = `summary` e `dominio` = slug do domínio da operação relacionada — sem o slug a página não monta o link.

- [ ] **Step 4: Implementar `montarCurl`**

Linhas, nesta ordem: `curl -s` (mais `-i` quando há `Location` ou quando a resposta é `204`), `-X <METODO>` quando não for `GET`, `"<servidor><rota com valores>"`, `-b cookies.txt` quando `cookieAuth`, `-H "X-Admin-Token: SEU_ADMIN_TOKEN"` quando `adminToken`, `-H "Content-Type: application/json"` + `-d '<corpo>'` quando JSON, `-F "<campo>=@<arquivo>"` por arquivo quando multipart. Query entra na URL na ordem de `parameters`, com `encodeURIComponent` no valor.

- [ ] **Step 5: Rodar e ver passar**

Run: `cd Projetos/fisga/docs-site && npm run test:unit`
Expected: PASS. Os casos que dependem dos 54 overrides usam fixtures inline neste task; a verificação contra os overrides reais fica no Task 5.

- [ ] **Step 6: Commit**

```bash
git add Projetos/fisga/docs-site/scripts
git commit -m "feat(docs-site): override por operação com erros humanos e curl executável"
```

### Task 4: Sintetizador de resposta e tabela de schema

**Files:**
- Modify: `Projetos/fisga/docs-site/scripts/gerar-referencia.mjs`
- Modify: `Projetos/fisga/docs-site/scripts/gerar-referencia.test.mjs`

**Interfaces:**
- Consumes: `gerarModelo` do Task 2.
- Produces:
  - `resolverRef(schema, contrato): object` — segue `$ref` até o schema final, com guarda de ciclo (`Set` de nomes).
  - `sintetizar(schema, contrato): { valor: unknown, notas: string[] }`
  - `montarRespostas(operacao, contrato): { respostas: Array<{ status, descricao, desfecho, mediaTypes, corpo, notas, location }>, sucesso: object | null, errosDerivados: Array<...> }` — `sucesso` é o próprio item de `respostas` com `desfecho: true` (o primeiro `2XX`/`3XX`), não uma cópia.
  - `montarTabelaDeSchema(schema, contrato): Array<{ caminho, tipo, obrigatorio, anulavel }>` — achata `object`/`array` até 1 nível de array, sem repetir `$ref`.
  - Cada `operacoes[operationId]` ganha `respostas`, `sucesso` e `request: { corpoTabela, corpoExemplo, arquivos }`. Parâmetros continuam só em `parametros` (com `valor`), no nível da operação — uma fonte por dado.

- [ ] **Step 1: Escrever os testes do sintetizador (falhando)**

Casos. `sintetizar` e `montarTabelaDeSchema` rodam com um `CONTRATO` fixture de `components.schemas`; `montarRespostas` e os casos de schema real rodam com `lerContrato()`/`listarOperacoes()` direto, **sem** `gerarModelo` — nesta altura os 54 overrides ainda não existem:

- `test('uuid, date, date-time e uri têm valores fixos', ...)`: `{type:'string',format:'uuid'}` → `'00000000-0000-4000-8000-000000000000'`; `date` → `'2026-11-21'`; `date-time` → `'2026-11-21T09:00:00Z'`; `uri` → `'https://fisga.abson.tech/exemplo'`.
- `test('tipo composto numérico vira número', ...)`: `{type:['number','string'],format:'double'}` → `0` (não `'0'`); `{type:['integer','string']}` → `0`.
- `test('enum escolhe o primeiro valor fora da sentinela', ...)`: `enum: ['NaoInformada','Clara']` → `'Clara'`; `enum: ['NaoSeAplica','Vazante']` → `'Vazante'`; `enum: ['NaoInformada']` → `'NaoInformada'`.
- `test('oneOf anulável vira o esqueleto do tipo com nota', ...)`: `{oneOf:[{type:'null'},{$ref:'#/components/schemas/UsuarioResponse'}]}` → objeto com as props de `UsuarioResponse` e nota contendo `pode ser`.
- `test('required anulável mostra o esqueleto, nunca null', ...)`: `{type:['null','string']}` → `''` com nota; nunca `null`.
- `test('objeto inclui todas as propriedades na ordem do schema', ...)`: `Object.keys(valor)` igual à ordem de `properties`.
- `test('array vira lista vazia com nota', ...)`: `{type:'array',items:{...}}` → `[]` e nota mencionando lista.
- `test('binary não aparece', ...)`: `{type:'string',format:'binary'}` num objeto → a chave é omitida.
- `test('example do media type vence o sintetizador', ...)`: `montarRespostas` de uma operação com `examples.semSessao.value` devolve `corpo` igual ao `JSON.stringify` do exemplo, indentado.
- `test('204 e text/html ficam sem bloco', ...)`: `corpo === null` e `notas` explicam; `mediaTypes` ainda lista o tipo quando existe.
- `test('201 com Location marca o header na linha de status', ...)`: `respostas[0].location === true`.
- `test('302 sem 2XX é desfecho e não entra em erros', ...)`: para `confirmarLink` real, `sucesso.status === '302'`, `sucesso.desfecho === true`, `corpo === null`, e `erros` não contém `302`.
- `test('tabela de schema achata objeto e array', ...)`: para `PescariaDetalhadaDto` real, existe a linha `entrada.local` (objeto aninhado) e `capturas[].especie` (array), e `entrada` não aparece sozinho como linha de tipo objeto.
- `test('tabela marca obrigatoriedade e anulabilidade', ...)`: `MeResponse.usuario` é `obrigatorio: true` e `anulavel: true`; `EntradaDePescaria.local` é `obrigatorio: false`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `cd Projetos/fisga/docs-site && npm run test:unit`
Expected: FAIL (`sintetizar is not a function`).

- [ ] **Step 3: Implementar `resolverRef` e `sintetizar`**

`resolverRef` segue `$ref` de `#/components/schemas/<Nome>` com um `Set` de nomes visitados; ciclo → devolve `{}` e adiciona nota `ciclo no schema`. `sintetizar` decide nesta ordem: `example`/`examples` (só em `montarRespostas`, não aqui) → `$ref` → `oneOf` (ignora o `null`, sintetiza o resto, nota `este campo pode ser null`) → `type` array (remove `null`; se sobrar 1, sintetiza; se sobrar mais de 1 e incluir `string`, número `0`) → `enum` → `format` (`uuid`/`date`/`date-time`/`uri`/`binary`) → `type` (`string` → `''`, `integer`/`number` → `0`, `boolean` → `false`, `object`, `array`).

`notas` são frases em pt-BR prontas para a página: `"este campo pode ser null"`, `"a lista vem vazia quando não há itens"`.

- [ ] **Step 4: Implementar `montarRespostas`, `montarTabelaDeSchema` e o `request`**

- Status ordenados numericamente; `>= 400` alimentam `errosDerivados` (mesma regra do Task 2).
- `sucesso` = primeiro `2XX`/`3XX`; se nenhum, `null` e a página usa o `302` como desfecho.
- `corpo` = `JSON.stringify(valor, null, 2)`; `null` quando `204`, quando o media type é `text/html`/`application/pdf`, quando o schema tem `format: 'binary'` na raiz ou quando não há `content`.
- `notas` acumula as notas do sintetizador, sem repetir.
- `location: true` quando o status é `201`/`202` e a resposta declara o header `Location`.
- `montarTabelaDeSchema` percorre **todas** as propriedades em todo nível (o contrato não descreve 327 propriedades à toa) e marca `obrigatorio` conforme `required` do pai — é o que faz `EntradaDePescaria.local` sair com `obrigatorio: false` e `MeResponse.usuario` com `true`; arrays viram `campo[]` e descem em `items` só quando `items` é objeto; profundidade máxima 6; ciclo de `$ref` volta ao mesmo nome e para.
- `request.corpoTabela` é `montarTabelaDeSchema` do schema do `requestBody` JSON; `request.corpoExemplo` é o `corpo` do override, já validado; `request.arquivos` idem.

- [ ] **Step 5: Rodar e ver passar; conferir o esqueleto da maior resposta**

Run:
```bash
cd Projetos/fisga/docs-site && npm run test:unit
node -e "import('./scripts/gerar-referencia.mjs').then(m=>{const c=m.lerContrato();const o=m.listarOperacoes(c).find(x=>x.operationId==='obterCondicoes');const r=m.montarRespostas(o.operacao,c);console.log(JSON.stringify(r.sucesso.corpo).length, r.sucesso.notas.length)})"
```
Expected: PASS, e o maior esqueleto (`obterCondicoes`) tem corpo não vazio e pelo menos uma nota (as listas vêm vazias).

- [ ] **Step 6: Commit**

```bash
git add Projetos/fisga/docs-site/scripts
git commit -m "feat(docs-site): esqueleto de resposta derivado do schema, rotulado"
```

### Task 5: Os 54 overrides

**Files:**
- Create: `Projetos/fisga/docs/publico/operacoes/<operationId>.md` (54 arquivos)
- Modify: `Projetos/fisga/docs-site/scripts/gerar-referencia.test.mjs` (habilitar os casos de contrato real: `confirmarLink` sem `302` em `erros`, `acesso` das 54 operações conferido contra `security`, e um `curl` real sem `undefined`)
- Modify: `Projetos/fisga/docs-site/package.json` (`preparar` passa a incluir o gerador)

**Interfaces:**
- Consumes: `validarOverride`, `auditar`, `montarCurl` do Task 3.
- Produces: os 54 arquivos que `npm run gerar` exige; nenhum outro artefato.

- [ ] **Step 1: Escrever o override de referência e rodar o gerador para ver a lista de pendências**

Criar `Projetos/fisga/docs/publico/operacoes/obterSaude.md`:

```markdown
---
operacao: obterSaude
proposta: >
  Informa se a API está no ar e qual versão está publicada.
acesso: publico
verificar: >
  O corpo traz `status: "ok"` e a versão do build.
---
Esta rota não consulta o banco nem o cache. É a primeira chamada de qualquer
monitoramento.
```

Run: `cd Projetos/fisga/docs-site && npm run gerar`
Expected: sai com código 1 e lista as 53 operações sem override, uma por linha, mais as pendências de `erros` que cada uma exigir.

- [ ] **Step 2: Escrever os 54 arquivos, domínio por domínio, na ordem canônica**

O diretório `Projetos/fisga/docs/publico/operacoes/` **não existe no git** hoje
(nem vazio): o primeiro `write` cria os diretórios-pai. Os 54 arquivos são
criados um por operação, com nome `<operationId>.md` — o validador recusa
qualquer outro nome. `lerOverrides` trata diretório inexistente como lista vazia
(assim o `npm run gerar` do Step 1 lista as 54 pendências em vez de estourar um
`ENOENT`), e o `gerarModelo` é quem cobra a cobertura.

Tabela de referência do que cada arquivo precisa declarar (derivada do contrato; `acesso` é decisão editorial):

| Domínio | Operação | `acesso` | Campos além de `proposta`/`verificar` | `erros` |
| --- | --- | --- | --- | --- |
| Sistema | `obterSaude` | publico | — | — |
| Sistema | `obterProntidao` | publico | — | — (503 é derivado: corpo vazio) |
| Sistema | `versoesLegais` | publico | — | — |
| Catálogo | `obterCatalogo` | publico | — | — |
| Autenticação | `solicitarLink` | publico | `corpo` | 400 |
| Autenticação | `registrarConta` | publico | `corpo` | 400 |
| Autenticação | `entrarComSenha` | publico | `corpo` | 400 |
| Autenticação | `confirmarEmail` | publico | `corpo` | 400 |
| Autenticação | `esquecerSenha` | publico | `corpo` | 400 |
| Autenticação | `redefinirSenha` | publico | `corpo` | 400 |
| Autenticação | `confirmarLink` | capacidade | `consulta` | — |
| Autenticação | `googleDisponivel` | publico | — | — |
| Autenticação | `iniciarLoginGoogle` | publico | — | 503 |
| Autenticação | `concluirLoginGoogle` | publico | — | — |
| Autenticação | `conectarGoogle` | sessao | — | 503 |
| Autenticação | `concluirConexaoGoogle` | sessao | — | — |
| Autenticação | `obterSessao` | publico | — | — |
| Autenticação | `sair` | publico | — | — |
| Autenticação | `sairDeTodos` | sessao | — | — |
| Autenticação | `atualizarPerfil` | sessao | `corpo` | 400 |
| Conta | `registrarConsentimento` | sessao | `corpo` | 400 |
| Conta | `alterarSenha` | sessao | `corpo` | 400 |
| Conta | `listarSessoes` | sessao | — | — |
| Conta | `exportarConta` | sessao | — | — |
| Conta | `excluirConta` | sessao | `corpo` | 400 |
| Pescarias | `criarPescaria` | sessao | `corpo` | 400 |
| Pescarias | `obterPescariaPorToken` | capacidade | `caminho` | 404 |
| Pescarias | `obterPermissaoPescaria` | capacidade | `caminho` | 404 |
| Pescarias | `obterPescariaDoDono` | dono | `caminho` | 404 |
| Pescarias | `atualizarPescaria` | dono | `corpo`, `caminho` | 400, 404 |
| Pescarias | `exportarPescariaPdf` | capacidade | `caminho` | 404 |
| Diário | `obterDiario` | sessao | — | — |
| Condições | `obterCondicoes` | publico | `consulta` | 400 |
| Condições | `compararCondicoes` | sessao | `consulta` | 400 |
| Locais | `reverterCoordenadas` | publico | `consulta` | 400 |
| Locais | `buscarLocais` | publico | `consulta` | — |
| Locais | `listarLocais` | sessao | — | — |
| Locais | `criarLocal` | sessao | `corpo` | 400 |
| Locais | `atualizarLocal` | dono | `corpo`, `caminho` | 400, 404 |
| Locais | `excluirLocal` | dono | `caminho` | 404 |
| Locais | `definirLocalPadrao` | dono | `caminho` | 404 |
| Pontos | `listarPontos` | sessao | — | — |
| Pontos | `criarPonto` | sessao | `corpo` | 400 |
| Pontos | `atualizarPonto` | dono | `corpo`, `caminho` | 400, 404 |
| Pontos | `excluirPonto` | dono | `caminho` | 404 |
| Trilhas | `listarTrilhas` | sessao | — | — |
| Trilhas | `criarTrilha` | sessao | `corpo` | 400 |
| Trilhas | `atualizarTrilha` | dono | `corpo`, `caminho` | 400, 404 |
| Trilhas | `excluirTrilha` | dono | `caminho` | 404 |
| Mídia | `enviarMidia` | sessao | `arquivos` | 400 |
| Espécies | `sugerirEspecies` | sessao | `arquivos` | 400 |
| Newsletter | `enviarNewsletter` | admin | — | — (404 é derivado) |
| Newsletter | `previsualizarNewsletter` | admin | `consulta` | — (404 é derivado) |
| Newsletter | `descadastrarNewsletter` | capacidade | `consulta` | — |

Regras de conteúdo, todas verificáveis no diff:

- `proposta` em 1–3 frases, presente do indicativo, sem "basta"/"simplesmente", descrevendo o efeito no mundo.
- `verificar` diz o que olhar no corpo ou no header, não repete a rota.
- `corpo` tem **todas** as propriedades `required` do schema. Para `EntradaDePescaria` (sem `required`) o mínimo que passa em `Pescaria.Validar` é `local`, `ambiente`, `data` e `periodo`: os quatro sem valor fazem `pescaria_sem_local`, `ambiente_invalido`, `data_invalida` e `periodo_invalido`. `mare`, `lua`, `clima`, `clarezaDaAgua`, `privacidadeDoLocal` e `capturas` têm default no record e podem ficar de fora; o exemplo documentado inclui `mare` e `privacidadeDoLocal` porque ensinam o uso, não porque sejam obrigatórios.
- `caminho` usa UUID `00000000-0000-4000-8000-000000000001` para `id` e `"tokenDeExemplo"` para `shareToken`.
- `consulta` de `obterCondicoes`/`compararCondicoes` usa `lat: -23.55, lon: -46.63, nome: "Represa de Guarapiranga"`; `reverterCoordenadas` usa `lat`/`lon` de cidade; `buscarLocais` usa `q: "Guarapiranga"`.
- `erros[].codigos` lista só códigos que a operação realmente devolve, tirados do código-fonte (ex.: `criarPescaria` → `data_invalida`, `local_longo`, `ambiente_invalido`, `periodo_invalido`, `muitas_capturas`, `captura_*`, `local_sem_coordenadas`).
- `arquivos: { arquivo: 'foto.jpg' }` em `enviarMidia` e `sugerirEspecies`.
- `enviarNewsletter` é a exceção conhecida: o handler lê `mes` e `dryRun` da query, mas o contrato não declara `parameters` nessa operação. O override **não** pode inventar `consulta` (o validador recusa chave fora de `parameters`); a página mostra "sem parâmetros" e a lacuna fica registrada em `inventario.md`, no Task 9, para o próximo PR de contrato.
- Corpo do Markdown: no máximo um parágrafo curto, sem heading, sem tabela.
- `proposta` de operação de escrita não promete `200`: o contrato manda (`201`, `202` ou `204`), e a página mostra o status real.

- [ ] **Step 3: Ligar o gerador ao `preparar` e rodar até ficar limpo**

Em `docs-site/package.json`, `"preparar": "npm run tokens && npm run gerar"` — a partir daqui `predev`, `prebuild`, `precheck` e `pretest:unit` regeneram o modelo, e o CI não precisa chamar o gerador direto. (Antes deste passo o `preparar` só copiava tokens, porque ainda não havia overrides e o gerador falharia.)

Run:
```bash
cd Projetos/fisga/docs-site && npm run gerar && npm run test:unit
cd /Users/absondutragalvao/.opencode/worktrees/issue-73-portal-referencia && npx --yes markdownlint-cli2@0.24.1
```
Expected: `gerar` escreve `src/data/referencia.json` sem pendências; `test:unit` PASS com os casos de contrato real habilitados; markdownlint 0 issues — o `overrides` de `MD041` já entrou no Task 1, então os arquivos com prosa passam; os que só têm frontmatter também passam, porque sem corpo não há primeira linha para acusar.

- [ ] **Step 4: Conferir a contagem e o tamanho**

Run:
```bash
cd Projetos/fisga/docs-site && node -e "
import('./scripts/gerar-referencia.mjs').then(m=>{
  const c=m.lerContrato(); const ov=m.lerOverrides(); const mo=m.gerarModelo(c,new Map(ov.map(o=>[o.operacao,o])));
  console.log('operacoes', Object.keys(mo.operacoes).length);
  console.log('dominios', mo.dominios.length);
  console.log('erros derivados', Object.values(mo.operacoes).reduce((n,o)=>n+o.erros.filter(e=>e.origem==='contrato').length,0));
  console.log('erros override', Object.values(mo.operacoes).reduce((n,o)=>n+o.erros.filter(e=>e.origem==='override').length,0));
})"
```
Expected: `54`, `13`, `115`, `37`.

Os casos de contrato real que entram neste task, no `gerar-referencia.test.mjs`:

- `test('os 54 overrides reais produzem um modelo sem pendências', ...)`: `lerOverrides()` + `gerarModelo(lerContrato(), ...)` não lança; `contagem` é `{ operacoes: 54, dominios: 13, paginas: 69 }`.
- `test('confirmarLink real não tem 302 em erros', ...)`: `operacoes.confirmarLink.erros` não contém `302` e `sucesso.status === '302'`.
- `test('acesso do override é compatível com security nas 54', ...)`: percorre `operacoes` e confere `acesso` × `security` de cada uma — é a rede que pega override copiado de outra operação.
- `test('nenhum curl real sai com undefined', ...)`: todo `exemplo` é `string[]` não vazio, sem `undefined`/`NaN`, e o das 29 de sessão contém `-b cookies.txt`.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/docs/publico/operacoes Projetos/fisga/docs-site/scripts
git commit -m "docs(portal): override das 54 operações com exemplo executável e erros"
```

### Task 6: Navegação, índice da referência e páginas de domínio

**Files:**
- Create: `Projetos/fisga/docs-site/src/components/Nav.astro`
- Create: `Projetos/fisga/docs-site/src/components/SeloDeStatus.astro`
- Modify: `Projetos/fisga/docs-site/src/components/Base.astro`
- Modify: `Projetos/fisga/docs-site/src/pages/index.astro`
- Create: `Projetos/fisga/docs-site/src/pages/referencia.astro`
- Create: `Projetos/fisga/docs-site/src/pages/referencia/[dominio].astro`
- Modify: `Projetos/fisga/docs-site/src/styles/portal.css`

**Interfaces:**
- Consumes: `src/data/referencia.json` com `{ info, dominios, operacoes, contagem }` do Task 2/3; `Base` do Task 1.
- Produces: 15 HTML (`index.html`, `referencia.html`, 13 `referencia/<slug>.html`); `Nav.astro` com prop `{ dominios, ativo?: string }`.
- Produces: `SeloDeStatus.astro` com props `{ metodo: string, status?: string }`, usado pelo Task 7.

- [ ] **Step 1: Escrever `SeloDeStatus.astro` e `Nav.astro`**

`SeloDeStatus`: `<span class="selo selo-get|selo-post|...">GET</span>` com texto sempre visível, contraste vindo de `--accent`/`--danger`/`--success`; nunca só cor. Com `status`, emite um segundo selo só com o número (`201`, `404`), sem cor semântica — o status é dado, não julgamento.

`Nav` recebe `dominios` (array de `{ slug, titulo, descricao, total }`) e `ativo`. Renderiza `<nav aria-label="Domínios da referência">` com uma lista de links `/referencia/<slug>`, `aria-current="page"` no `ativo`, e `<span class="nav-count">` com `total` em `--muted`. No mobile (<900px) vira uma faixa horizontal com `overflow-x: auto`, alvos de 44px.

- [ ] **Step 2: Trocar a home provisória pela home derivada de `info`**

`index.astro` lê `referencia.json` (import estático) e renderiza com `Base`: `<h1>` = `info.titulo`, parágrafo = primeiro parágrafo de `info.descricao` (dividir em `\n\n` e usar o primeiro), linha "Versão do contrato: `info.versao`", dois cartões de caminho — `/referencia` ativo e `/guia-rapido` desabilitado com `<span class="chip">Em breve</span>` — e a faixa "Leitura pública. Escrita exige sua conta." com link para `/referencia/autenticacao/entrarComSenha`.

- [ ] **Step 3: Escrever `referencia.astro` (índice)**

`<h1>Referência da API</h1>`, um parágrafo curto, e os 13 domínios em cartões (`<h2>` = título da tag, `<p>` = descrição da tag, contagem de operações, link "Ver operações"). Nada de lista de 54 operações nesta página — o índice de domínio lista.

- [ ] **Step 4: Escrever `referencia/[dominio].astro`**

`getStaticPaths()` devolve um item por domínio com `params: { dominio: slug }, props: { dominio }`. A página usa layout de duas colunas: `Nav` à esquerda e, no artigo, `<h1>` = título da tag, descrição da tag, e a lista de operações do domínio na ordem de `paths`, cada uma com `SeloDeStatus` do método (texto, não só cor), `summary`, a rota em `code` e o link para `/referencia/<slug>/<operationId>`.

- [ ] **Step 5: Buildar e conferir as 15 páginas**

Run:
```bash
cd Projetos/fisga/docs-site && npm run build && find dist -name '*.html' | sort
```
Expected: `dist/index.html`, `dist/referencia.html`, `dist/referencia/{sistema,catalogo,autenticacao,conta,pescarias,diario,condicoes,locais,pontos,trilhas,midia,especies,newsletter}.html` — 15 arquivos, nenhum `referencia/index.html`.

- [ ] **Step 6: Conferir a navegação e a CSP**

Run:
```bash
cd Projetos/fisga/docs-site && grep -o '<a[^>]*aria-current="page"[^>]*>' dist/referencia/conta.html && \
  ! grep -l '<script[^>]*>' dist/*.html dist/referencia/*.html
```
Expected: um link com `aria-current="page"` na página de domínio ativo; nenhum HTML com `<script>` — nesta altura o site não tem JS nenhum. O botão copiar entra no Task 7 como módulo externo, e o teste do Task 8 cobra `src` em todo `<script>`.

- [ ] **Step 7: Commit**

```bash
git add Projetos/fisga/docs-site/src
git commit -m "feat(docs-site): navegação, índice e páginas de domínio"
```

### Task 7: Página de operação com as 11 seções

**Files:**
- Create: `Projetos/fisga/docs-site/src/components/BlocoDeCodigo.astro`
- Create: `Projetos/fisga/docs-site/src/components/PreRequisitoDeSessao.astro`
- Create: `Projetos/fisga/docs-site/src/components/TabelaDeErros.astro`
- Create: `Projetos/fisga/docs-site/src/components/TabelaDeSchema.astro`
- Create: `Projetos/fisga/docs-site/src/pages/referencia/[dominio]/[operacao].astro`
- Modify: `Projetos/fisga/docs-site/src/styles/portal.css`

**Interfaces:**
- Consumes: `operacoes[operationId]` do Task 2/3/4 (`metodo`, `rota`, `summary`, `descricao`, `proposta`, `acesso`, `acessoTexto`, `autenticacao`, `consentimentoTexto`, `limites`, `xCache`, `parametros`, `request`, `respostas`, `sucesso`, `erros`, `exemplo`, `verificar`, `notasHtml`, `relacionados`).
- Produces: 54 `referencia/<slug>/<operationId>.html`; `BlocoDeCodigo.astro` com props `{ rotulo, linguagem, linhas: string[], copiavel?: boolean }`; `TabelaDeErros.astro` com `{ erros }`; `TabelaDeSchema.astro` com `{ linhas, titulo }`; a página usa `SeloDeStatus.astro` e `Nav.astro` do Task 6 com `{ metodo, status? }` e `{ dominios, ativo }`.

- [ ] **Step 1: Escrever `BlocoDeCodigo.astro`**

`<figure>` com `<figcaption>` (rótulo), `<pre><code class="language-<linguagem>">` com as linhas unidas por `\n`, e um `<button type="button" data-copiar>` quando `copiavel`. O script vai num `<script>` processado pelo Astro (nunca `is:inline`), registra um listener delegado em `document` e usa `navigator.clipboard.writeText`; em falha, não faz nada (o texto continua selecionável). O botão é o único JS do site.

- [ ] **Step 2: Escrever `PreRequisitoDeSessao.astro`**

Props `{ operacaoDeEntrada: { href, titulo } }`. Renderiza um `<aside class="pre-requisito" aria-labelledby="pre-req">` com `<h2 id="pre-req">Antes de chamar</h2>` e a copy fixa: "Estes exemplos usam o cookie `fisga_auth` salvo em `cookies.txt`. Se você ainda não tem, faça `POST /api/auth/entrar` com `curl -c cookies.txt` antes de rodar o exemplo abaixo." — com link para a página de `entrarComSenha`. Aparece só quando `autenticacao === 'sessao'`.

- [ ] **Step 3: Escrever `TabelaDeErros.astro` e `TabelaDeSchema.astro`**

`TabelaDeErros`: tabela com colunas `Status`, `codigo`, `Quando acontece`. `codigo` mostra `—` quando `null`, e para `codigos` múltiplos mostra um `<code>` por linha dentro da célula. `origem === 'override'` ganha `<span class="origem">documentado</span>` com `title="O contrato não declara este código; a informação vem da página da operação."`. Nunca colore a linha inteira: o status é texto.

`TabelaDeSchema`: tabela `Campo`, `Tipo`, `Obrigatório`, `Aceita null`. `Campo` em `<code>`; `Tipo` derivado do modelo (`string`, `number`, `integer`, `boolean`, `object`, `array`, enum → lista de valores); `Obrigatório` = "sim"/"não"; `Aceita null` = "sim" quando `anulavel`. Campo obrigatório que aceita null ganha uma nota abaixo da tabela, uma vez: "Campos marcados como obrigatórios podem vir `null`; o exemplo mostra a forma."

- [ ] **Step 4: Escrever a página de operação**

`getStaticPaths()` percorre os domínios na ordem canônica e as operações de cada um (o `Nav` precisa dos 13 domínios em toda página, então ele recebe `dominios` inteiro do JSON, não só o atual); `params: { dominio, operacao }`, `props: { operacao: modelo }`. Seções, na ordem da política editorial:

1. `SeloDeStatus` + `<h1>` = `summary` + linha `metodo rota` em `code`.
2. Propósito: `proposta`.
3. Acesso: duas linhas separadas — "Autenticação: <autenticacaoTexto>" (do contrato: "sem autenticação", "cookie de sessão `fisga_auth`" ou "header `X-Admin-Token`") e "Quem pode chamar: <acessoTexto>" (do override) — mais `consentimentoTexto` quando a operação está em `consentimento`.
4. Limites: rate limit (`policy`, `requests`, `windowSeconds`) ou "sem política de rate limit declarada"; `Cache-Control` sempre.
5. Request: tabela de parâmetros com colunas `Nome`, `Local`, `Tipo`, `Obrigatório` e `Exemplo` (o valor do override, ou `—`); `TabelaDeSchema` do `corpoTabela`, seguida do `corpoExemplo` em `BlocoDeCodigo`; `arquivos` em tabela `Campo`/`Arquivo do exemplo`. A `TabelaDeSchema` é só para schema (`Campo`, `Tipo`, `Obrigatório`, `Aceita null`); parâmetro tem local e exemplo e não cabe nela. Sem parâmetro, sem `requestBody` e sem `arquivos`, a seção diz "Sem parâmetros, corpo ou arquivos." em vez de renderizar três tabelas vazias.
6. Sucesso/desfecho: status, `descricao`, header `Location` quando marcado, `BlocoDeCodigo` com o esqueleto e o rótulo fixo "Exemplo derivado do schema — mostra a forma, não dados reais."; para `302`, "Desfecho: `302`" e o texto do contrato, sem URL inventada; sem bloco para `204`, `text/html` e `application/pdf`, com o media type declarado.
7. Erros: `TabelaDeErros` com `erros`; quando vazio, "Sem erro declarado no contrato."; `302` nunca entra aqui.
8. Exemplo executável: `PreRequisitoDeSessao` quando `cookieAuth`, a linha de base — "Base: `<servidor>` (Produção). Para rodar local, troque por `<servidores[1].url>` (Local)." —, e `BlocoDeCodigo` com `linguagem: 'bash'` e `exemplo`. Quando `metodo !== 'GET'`, antes do bloco, a frase fixa: "Estes exemplos apontam para produção e alteram dados reais da conta. Rode contra a API local se estiver testando." Sem `servidores[1]`, a linha mostra só a base.
9. Como verificar: `verificar`.
10. Comportamento e efeitos: `descricao` do contrato em parágrafos + `notasHtml` (corpo renderizado).
11. Relacionados: lista de links; oculta quando vazia.

Layout de três colunas (o protótipo confirmou que cabe em 1120px): `Nav.astro` do Task 6 à esquerda com `ativo = <dominio>`, o artigo no meio e, à direita, `<nav aria-label="Nesta página">` com âncoras para as seções. Abaixo de 900px vira uma coluna; a navegação de domínios vira a faixa horizontal do Task 6 e a de seções some (o artigo é curto no mobile). A página de operação passa `ativo` para o `Nav`, então o domínio continua marcado com `aria-current="page"`.

- [ ] **Step 5: Buildar e conferir as 69 páginas**

Run:
```bash
cd Projetos/fisga/docs-site && npm run build && find dist -name '*.html' | wc -l && \
  find dist -name '*.html' | grep -c '^dist/referencia/[a-z]*/' && \
  ls dist/referencia/conta/listarSessoes.html dist/referencia/autenticacao/entrarComSenha.html
```
Expected: `69`; `54` arquivos em `referencia/<slug>/`; os dois caminhos existem.

- [ ] **Step 6: Conferir os cinco casos-limite no HTML gerado**

Run:
```bash
cd Projetos/fisga/docs-site
grep -q 'Desfecho' dist/referencia/autenticacao/confirmarLink.html
grep -q 'Exemplo derivado do schema' dist/referencia/conta/listarSessoes.html
grep -q 'Antes de chamar' dist/referencia/conta/listarSessoes.html
grep -q 'Sem erro declarado no contrato' dist/referencia/sistema/obterSaude.html
grep -q 'text/html' dist/referencia/newsletter/previsualizarNewsletter.html
grep -q 'arquivo=@foto.jpg' dist/referencia/midia/enviarMidia.html
grep -q 'publico, max-age=86400' dist/referencia/catalogo/obterCatalogo.html
```
Expected: os sete `grep -q` saem com 0. Nenhum deles deve encontrar `Location` em `confirmarLink.html` nem `Antes de chamar` em `obterSaude.html`:
```bash
! grep -q 'Location' dist/referencia/autenticacao/confirmarLink.html
! grep -q 'Antes de chamar' dist/referencia/sistema/obterSaude.html
```

- [ ] **Step 7: Commit**

```bash
git add Projetos/fisga/docs-site/src
git commit -m "feat(docs-site): página de operação com 11 seções e aviso de sessão"
```

### Task 8: Teste pós-build das 69 páginas

**Files:**
- Create: `Projetos/fisga/docs-site/scripts/verificar-paginas.test.mjs`

**Interfaces:**
- Consumes: `dist/` do Task 7 e `src/data/referencia.json` (para saber quais operações exigem tabela de erro e aviso de sessão) — o JSON, não `gerarModelo`, que exige os 54 overrides.
- Produces: `npm run test:paginas` verde em `dist/` completo; falha ruidosa quando falta página.

- [ ] **Step 1: Escrever o teste com as asserções por tipo de página**

O arquivo lê `dist/` com `readFileSync` e `existsSync`. Se `dist/` não existir, falha com a mensagem `rode "npm run build" antes de "npm run test:paginas"`. Asserções:

- **69 páginas**: `index.html`, `referencia.html`, 13 `referencia/<slug>.html`, 54 `referencia/<slug>/<operationId>.html`, contadas a partir de `src/data/referencia.json` (não de `gerarModelo`: ele exige os 54 overrides e lançaria), e nenhum HTML além desses.
- **Toda página**: existe; tem exatamente um `<h1`; tem `<title>` não vazio; tem `<main`; tem `<a class="skip-link"`; `lang="pt-BR"`; nenhum `<script` sem `src=`; nenhum `style="` com valor de cor (`#` ou `rgb(`); o `href` do `<link rel="canonical">` é `https://docs.fisga.abson.tech` + o caminho do arquivo sem `.html` (e `/` para `index.html`), nunca contendo `.html`.
- **Operação**: contém `id="<operationId>"` no `<h1>`; contém o bloco de exemplo (`<pre`); contém o aviso "Estes exemplos apontam para produção" exatamente quando `metodo !== 'GET'`; contém tabela de erro quando `erros.length > 0`; contém "Antes de chamar" exatamente quando `autenticacao === 'sessao'`; contém "Sem erro declarado no contrato" quando `erros.length === 0`; contém "Exemplo derivado do schema" quando `sucesso?.corpo` não é `null`; contém o media type quando `sucesso?.mediaTypes` inclui `text/html` ou `application/pdf`.
- **Domínio**: contém um link para cada operação do domínio.
- **Home**: contém `info.titulo` e a versão do contrato.
- **CSS**: existe exatamente um `.css` por página referenciado em `_astro/`; nenhum HTML referencia `.woff2` fora do CSS.

- [ ] **Step 2: Rodar contra o build atual e ver passar**

Run: `cd Projetos/fisga/docs-site && npm run test:paginas`
Expected: PASS, com um teste cujo nome reporta `69 páginas` no relatório do `node --test`.

- [ ] **Step 3: Provar que o teste falha quando falta página**

Run:
```bash
cd Projetos/fisga/docs-site && mv dist/referencia/conta/listarSessoes.html /tmp/listarSessoes.html && \
  (npm run test:paginas; echo "exit=$?") ; mv /tmp/listarSessoes.html dist/referencia/conta/listarSessoes.html
```
Expected: `exit=1` e a mensagem nomeia `referencia/conta/listarSessoes.html`.

- [ ] **Step 4: Provar que o teste pega `<script>` sem `src`**

Run:
```bash
cd Projetos/fisga/docs-site && cp dist/index.html /tmp/index.html.bak && \
  perl -0pi -e 's#</body>#<script>void 0;</script></body>#' dist/index.html && \
  (npm run test:paginas; echo "exit=$?") ; cp /tmp/index.html.bak dist/index.html
```
Expected: `exit=1` com a mensagem sobre `script` sem `src`; depois `npm run test:paginas` volta a passar.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/docs-site/scripts/verificar-paginas.test.mjs
git commit -m "test(docs-site): verificação pós-build das 69 páginas"
```

### Task 9: CI, markdownlint e documentação do programa

**Files:**
- Modify: `.github/workflows/fisga-pipeline.yml`
- Modify: `.github/workflows/fisga-deploy.yml`
- Modify: `.markdownlint-cli2.jsonc`
- Modify: `Projetos/fisga/docs/api/portal.md`
- Modify: `Projetos/fisga/docs/api/inventario.md`
- Modify: `Projetos/fisga/docs/api/politica-editorial.md`
- Modify: `Projetos/fisga/docs/api/backlog.md`
- Modify: `Projetos/fisga/docs/ci-cd.md`
- Modify: `Projetos/fisga/docs/INDEX.md`
- Modify: `Projetos/fisga/AGENTS.md`
- Modify: `Projetos/fisga/README.md`

**Interfaces:**
- Consumes: scripts e site dos Tasks 1–8.
- Produces: job `docs-site` no `gate`; docs alinhadas; arquivo de spec e plano linkados no `INDEX`.

- [ ] **Step 1: Excluir `docs-site/**` do filtro de código nos dois workflows**

Em `fisga-pipeline.yml`, no `CODIGO`:

```bash
grep -vE '^Projetos/fisga/(docs/|docs-site/|README\.md$|AGENTS\.md$)' || true
```

Em `fisga-deploy.yml`, no `CODIGO` (linha ~44): a mesma exclusão `docs-site/`. Em ambos, `DOCUMENTACAO` ganha `^Projetos/fisga/docs-site/.*\.md$` para que PR de README do site rode o job `docs`.

- [ ] **Step 2: Criar o output `portal` e o job `docs-site`**

Em `changes.outputs`: `portal: ${{ steps.diff.outputs.portal }}`. No script:

```bash
PORTAL="$(printf '%s\n' "$FILES" \
  | grep -E '^(Projetos/fisga/docs-site/|Projetos/fisga/docs/publico/|Projetos/fisga/docs/api/openapi\.json$)' || true)"
if [ -n "$PORTAL" ]; then echo "portal=true" >> "$GITHUB_OUTPUT"; else echo "portal=false" >> "$GITHUB_OUTPUT"; fi
```

`workflow_dispatch` também define `portal=true` — o ramo curto-circuita antes
de ler `FILES`, então sem essa linha um dispatch manual não buildaria o portal
(o `gate` ficaria verde sem o job).

Job novo, entre `docs` e `docker`:

```yaml
  docs-site:
    name: docs-site
    needs: changes
    if: needs.changes.outputs.portal == 'true'
    runs-on: ubuntu-latest
    timeout-minutes: 10
    defaults:
      run:
        working-directory: Projetos/fisga/docs-site
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: actions/setup-node@v7.1.0
        with:
          node-version: ${{ env.NODE_VERSION }}
          cache: npm
          cache-dependency-path: Projetos/fisga/docs-site/package-lock.json
      - run: npm ci
      - run: npm run test:unit
      - run: npm run build
      - run: npm run test:paginas
```

`gate.needs` passa a incluir `docs-site`. O `gate` usa `if: always()`, então o job pulado por `portal == 'false'` conta como `skipped` e não derruba o gate.

O job **não** instala browsers, não roda `astro check` e não usa cache de `.astro`:
a F2.4 cresce aqui. `npm ci` exige `package-lock.json` commitado no Task 1.

- [ ] **Step 3: Conferir o markdownlint com os 54 overrides e o README do site**

O `overrides` de `MD041` e o glob do `docs-site/README.md` já entraram no Task 1;
aqui é a primeira vez que eles rodam sobre os 54 arquivos reais.

Run: `npx --yes markdownlint-cli2@0.24.1`
Expected: 0 issues, agora incluindo os 54 overrides e o `docs-site/README.md`.

- [ ] **Step 4: Atualizar as docs de arquitetura e política**

- `docs/api/portal.md`: substituir "Operação individual: `/referencia/<dominio>#<operationId>`" por página própria; trocar "content collection" por JSON gerado; corrigir o formato do override (sem `consentimento`/`limites`/`cache`, com `proposta`, `caminho`, `consulta`, `corpo`, `arquivos`, `verificar`, `relacionados`, `erros`); corrigir a lista de falhas para as sete; documentar `script` como módulo com `assetsInlineLimit: 0`; registrar `try_files` como pendência da F2.5.
- `docs/api/inventario.md`: renomear as seções de domínio para os nomes das tags (`Sistema`, `Catálogo`, `Autenticação`, `Conta`, `Pescarias`, `Diário`, `Condições`, `Locais`, `Pontos`, `Trilhas`, `Mídia`, `Espécies`, `Newsletter`) nas duas tabelas, e manter a contagem 54. Em "Lacunas de contrato", registrar a única lacuna conhecida: `POST /api/newsletter/enviar` lê `mes` e `dryRun` da query sem `parameters` no contrato — o portal não inventa parâmetro; a correção é metadata da API, fora da F2.1.
- `docs/api/politica-editorial.md`: nota na seção 6 dizendo que, na F2.1, o "exemplo de resposta completo" é o esqueleto derivado rotulado; o exemplo real entra na F2.2.
- `docs/api/backlog.md`: marcar F2.1 como entregue e registrar o que ficou para F2.4 (astro check no CI, lychee no dist, orçamento de tempo, output para deploy).
- `docs/ci-cd.md`: novo invariante — PR que toca `docs-site/**`, `docs/publico/**` ou `docs/api/openapi.json` roda `docs-site`; deploy continua sem esses caminhos.
- `docs/INDEX.md`: nova entrada para o site e para o spec/plano.
- `Projetos/fisga/AGENTS.md`: ajustar o invariante "Documentação não builda nem deploya" com a exceção do portal.
- `Projetos/fisga/README.md`: uma linha em `## API` apontando o portal.

- [ ] **Step 5: Rodar as guardas locais**

Run:
```bash
cd /Users/absondutragalvao/.opencode/worktrees/issue-73-portal-referencia
npx --yes markdownlint-cli2@0.24.1
node Projetos/fisga/scripts/verificar-rotas.mjs
node --test Projetos/fisga/scripts/verificar-rotas.test.mjs
cd Projetos/fisga/docs-site && npm run build && npm run test:unit && npm run test:paginas
```
Expected: markdownlint 0; `verificar-rotas.mjs` com `54/54/54`; 10 testes da guarda PASS; build e os dois conjuntos de teste PASS.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows .markdownlint-cli2.jsonc Projetos/fisga
git commit -m "ci(docs-site): job do portal, markdownlint dos overrides e docs do programa"
```

### Task 10: Review final e fechamento do PR

**Files:**
- Modify: nenhum (só correções que o review apontar)

**Interfaces:**
- Consumes: tudo dos Tasks 1–9.

- [ ] **Step 1: Rodar a suíte completa na raiz do Fisga**

Run:
```bash
cd /Users/absondutragalvao/.opencode/worktrees/issue-73-portal-referencia
npx --yes markdownlint-cli2@0.24.1 && \
  node Projetos/fisga/scripts/verificar-rotas.mjs && \
  cd Projetos/fisga/docs-site && npm run check && npm run build && npm run test:unit && npm run test:paginas
```
Expected: tudo verde; `astro check` com 0 erros.

- [ ] **Step 2: Revisar o diff inteiro contra a spec**

Ler `git diff --stat main...HEAD` e conferir contra a Definição de pronto da spec, item por item. Registrar no corpo do PR quais itens foram verificados e como.

- [ ] **Step 3: Abrir o PR e acompanhar os checks**

```bash
export GH_TOKEN="$(gh auth token)"
git -c credential.helper= -c credential.helper='!f() { echo username=x-access-token; echo password=$GH_TOKEN; }; f' push -u origin issue-73-portal-referencia
gh pr create --fill --base main --head issue-73-portal-referencia \
  --title "feat(docs-site): portal da API com gerador da referência (F2.1)" \
  --body "Closes #73. Plano em docs/superpowers/plans/2026-11-21-f2-1-esqueleto-e-referencia.md."
```

Aguardar `gate` verde, responder e resolver todas as threads de review, e só então mergear.

- [ ] **Step 4: Liberar o claim e fechar a issue**

```bash
node ~/.opencode/scripts/claim-tarefa.mjs release 73
```

Mover a #73 para `Done` no quadro (project 6).
