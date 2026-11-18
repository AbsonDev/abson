# Boas-vindas — uma tela, uma pergunta — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trocar o wizard de boas-vindas por uma tela focada que faz a única pergunta que desbloqueia o produto: onde a pessoa pesca.

**Architecture:** A rota `/boas-vindas` vira um fluxo focado (sem header, rodapé ou tabbar) com saudação personalizada e um único controle: busca de local, com geolocalização e três sugestões. Nome e newsletter saem; o local salvo leva direto ao `/hoje`, que já abre com as condições. Nenhuma mudança de API.

**Tech Stack:** Astro 7, TypeScript, CSS com tokens existentes, Playwright (API mockada com `page.route`).

**Spec:** `docs/superpowers/specs/2026-11-18-boas-vindas-design.md`

## Global Constraints

- Toda cópia em pt-BR, exatamente como na spec.
- Usar apenas os tokens existentes (`--papel`, `--superficie`, `--tinta`, `--tinta-suave`, `--linha`, `--terracota`, `--creme`, Fraunces, Inter). Sem estética paralela.
- Nenhuma dependência nova e nenhuma mudança de API.
- `#boas-pular` continua existindo: `web/tests/identity-e2e.spec.ts` clica nele.
- Alvos de toque ≥ 44 px, label visível, `aria-live` no status, foco visível.
- `npm run check` deve terminar com 0 erros e `npm run test:e2e` com a suíte mockada verde.

## Review Focus

1. **Usuário de link mágico sem nome** (`usuario.nome: null`) — deve ver “Bem-vindo, pescador.”, nunca “Bem-vindo, .”.
2. **Falha de rede no `/api/auth/me`** — a tela deve mostrar uma mensagem em `role="status"` e não deixar a busca parecendo funcional.
3. **Latitude/longitude como string na busca** — o POST deve enviar números, nunca `NaN` nem string.
4. **Duas escolhas rápidas** — apenas um POST em `/api/locais`; o resto é ignorado enquanto salva.
5. **`sessionStorage['fisga:retorno']` externo** (`https://evil.example`) — deve cair em `/hoje`, nunca navegar para fora.

## File Structure

- Modify: `Projetos/fisga/web/src/layouts/Layout.astro` — prop `foco` para esconder header, rodapé e tabbar.
- Modify: `Projetos/fisga/web/src/styles/global.css` — regras de `body.foco`.
- Rewrite: `Projetos/fisga/web/src/pages/boas-vindas.astro` — markup, CSS e script da tela.
- Create: `Projetos/fisga/web/tests/boas-vindas.spec.ts` — suíte mockada da tela.
- No change: `Projetos/fisga/web/tests/identity-e2e.spec.ts` e `Projetos/fisga/web/tests/auth.spec.ts` (os ids usados continuam iguais).

---

### Task 1: A tela de uma pergunta (shell focado + saudação)

**Files:**
- Modify: `Projetos/fisga/web/src/layouts/Layout.astro:6-36,73`
- Modify: `Projetos/fisga/web/src/styles/global.css:302-310`
- Rewrite: `Projetos/fisga/web/src/pages/boas-vindas.astro`
- Test: `Projetos/fisga/web/tests/boas-vindas.spec.ts`

**Interfaces:**
- Consumes: `Layout` (Astro) e `Logo` (`../components/Logo.astro`).
- Produces: `#boas-titulo` (h1), `#boas-pular` (button), `#boas-busca-local` (input), `#boas-usar-localizacao` (button), `#boas-resultados` (ul), `#boas-local-status` (p `role="status"`), `.boas-sugestao[data-nome][data-cidade][data-estado][data-latitude][data-longitude][data-ambiente]`.

- [ ] **Step 1: Escrever a suíte que falha (shell, saudação e guards)**

```ts
// web/tests/boas-vindas.spec.ts
import { test, expect, type Page } from '@playwright/test';

const API = 'http://localhost:5184';
const USUARIO = {
  id: '1',
  email: 'pescador@fisga.app',
  nome: 'Marina Sá',
  nomeExibicao: 'Marina Sá'
};

async function mockMe(page: Page, usuario: unknown, extras: Record<string, unknown> = {}) {
  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        autenticado: Boolean(usuario),
        usuario,
        consentimentoPendente: false,
        googleConectado: false,
        ...extras
      })
    })
  );
}

async function mockPagina(page: Page, caminho: string) {
  await page.route(`**${caminho}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'text/html',
      body: `<!doctype html><h1>${caminho}</h1>`
    })
  );
}

test('mostra saudação, a pergunta e nenhum resquício do wizard antigo', async ({ page }) => {
  await mockMe(page, USUARIO);
  await page.goto('/boas-vindas?novo=1');

  await expect(page.locator('#boas-titulo')).toHaveText('Bem-vindo, Marina.');
  await expect(page.getByLabel('Onde você pesca?')).toBeVisible();
  await expect(page.locator('input[name="name"]')).toHaveCount(0);
  await expect(page.locator('#boas-novidades')).toHaveCount(0);
  await expect(page.locator('.boas-progresso')).toHaveCount(0);
  await expect(page.locator('header.topo')).toBeHidden();
  await expect(page.locator('footer.rodape')).toBeHidden();
});

test('sem nome, cumprimenta como pescador', async ({ page }) => {
  await mockMe(page, { ...USUARIO, nome: null, nomeExibicao: 'pescador@fisga.app' });
  await page.goto('/boas-vindas');
  await expect(page.locator('#boas-titulo')).toHaveText('Bem-vindo, pescador.');
});

test('sem sessão, vai para /entrar', async ({ page }) => {
  await mockMe(page, null);
  await page.goto('/boas-vindas');
  await expect(page).toHaveURL(/\/entrar\?voltar=\/hoje$/);
});

test('com aceite pendente, vai para /aceite com destino', async ({ page }) => {
  await mockMe(page, USUARIO, { consentimentoPendente: true });
  await page.goto('/boas-vindas?novo=1');
  await expect(page).toHaveURL(/\/aceite\?destino=%2Fboas-vindas%3Fnovo%3D1$/);
});

test('falha ao carregar a conta mostra status e não some', async ({ page }) => {
  await page.route(`${API}/api/auth/me`, (route) => route.abort());
  await page.goto('/boas-vindas');
  await expect(page.locator('#boas-local-status')).toHaveText(
    'Não foi possível carregar sua conta. Recarregue a página.'
  );
});
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm run test:e2e -- tests/boas-vindas.spec.ts`
Expected: FAIL — `#boas-titulo` não existe (a tela ainda é o wizard).

- [ ] **Step 3: Adicionar `foco` ao Layout**

Em `Layout.astro`, adicionar `foco?: boolean` à interface, `foco = false` no destructuring, `!foco` em `mostrarTabbar` e `${foco ? 'foco' : ''}` na classe do `<body>`. Em `global.css`, junto do bloco “Cabeçalho e rodapé”:

```css
body.foco .topo,
body.foco .rodape,
body.foco .tabbar {
  display: none;
}
```

- [ ] **Step 4: Implementar o shell da tela**

Em `boas-vindas.astro`, usar `<Layout ... foco>`; barra superior com `<Logo compacto />` e `<button id="boas-pular" type="button">Agora não</button>`; conteúdo com eyebrow `Primeiro acesso`, `<h1 id="boas-titulo">Bem-vindo.</h1>`, subtítulo da spec, label visível + input `#boas-busca-local`, botão `#boas-usar-localizacao`, `<ul id="boas-resultados" class="boas-resultados oculto">`, `<p id="boas-local-status" role="status" aria-live="polite">`, bloco de sugestões e linha de privacidade. CSS: `min-height: 100dvh`, coluna `min(100%, 620px)`, H1 `clamp(2.2rem, 5.5vw, 3.8rem)`, input 52 px, botões ≥ 44 px.

- [ ] **Step 5: Implementar a inicialização**

No `<script>`, manter `import { destinoSeguro } from '../lib/auth';` e:

```ts
const API = (document.body.dataset.api || 'http://localhost:5184').replace(/\/$/, '');
const titulo = document.getElementById('boas-titulo');
const status = document.getElementById('boas-local-status');

function primeiroNome(nome: string | null | undefined): string {
  const limpo = String(nome || '').trim();
  return limpo ? limpo.split(/\s+/)[0] : 'pescador';
}

async function iniciar(): Promise<void> {
  const resposta = await fetch(`${API}/api/auth/me`, { credentials: 'include' });
  const dados = await resposta.json().catch(() => ({}));
  if (!dados.autenticado || !dados.usuario) {
    window.location.replace('/entrar?voltar=/hoje');
    return;
  }
  if (dados.consentimentoPendente) {
    window.location.replace(
      '/aceite?destino=' + encodeURIComponent(window.location.pathname + window.location.search)
    );
    return;
  }
  if (titulo) titulo.textContent = `Bem-vindo, ${primeiroNome(dados.usuario.nome)}.`;
}
```

O `catch` de `iniciar` escreve a mensagem da falha em `#boas-local-status`. Usar `textContent` sempre que o valor vier do servidor.

- [ ] **Step 6: Rodar e ver passar**

Run: `npm run test:e2e -- tests/boas-vindas.spec.ts`
Expected: 5 passed.

- [ ] **Step 7: Commit**

```bash
git add Projetos/fisga/web/src/layouts/Layout.astro Projetos/fisga/web/src/styles/global.css Projetos/fisga/web/src/pages/boas-vindas.astro Projetos/fisga/web/tests/boas-vindas.spec.ts
git commit -m "feat(fisga): boas-vindas em uma tela focada"
```

---

### Task 2: Escolher o local (sugestões, busca e salvamento)

**Files:**
- Modify: `Projetos/fisga/web/src/pages/boas-vindas.astro` (markup das sugestões, script)
- Test: `Projetos/fisga/web/tests/boas-vindas.spec.ts`

**Interfaces:**
- Consumes: `#boas-busca-local`, `#boas-resultados`, `#boas-local-status` da Task 1.
- Produces: `type LocalEscolhido = { nome: string; cidade?: string | null; estado?: string | null; latitude: number; longitude: number; ambiente?: string }`; `salvarLocal(local: LocalEscolhido): Promise<void>`; `buscarLocal(termo: string): Promise<void>`; `destino(): string`.

- [ ] **Step 1: Escrever os testes que falham**

```ts
async function mockLocais(page: Page) {
  let corpo: Record<string, unknown> | null = null;
  await page.route(`${API}/api/locais`, async (route) => {
    if (route.request().method() === 'POST') {
      corpo = route.request().postDataJSON() as Record<string, unknown>;
      return route.fulfill({ status: 201, contentType: 'application/json', body: '{}' });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
  return () => corpo;
}

test('sugestão salva o local e segue para /hoje', async ({ page }) => {
  await mockMe(page, USUARIO);
  const corpo = await mockLocais(page);
  await mockPagina(page, '/hoje');
  await page.goto('/boas-vindas');

  await page.getByRole('button', { name: 'Aracaju' }).click();
  await expect(page).toHaveURL(/\/hoje$/);
  expect(corpo()).toEqual({
    nome: 'Aracaju',
    cidade: 'Aracaju',
    estado: 'SE',
    latitude: -10.911,
    longitude: -37.071,
    ambiente: 'Mar',
    padrao: true
  });
});

test('busca mostra resultado e escolher salva com números', async ({ page }) => {
  await mockMe(page, USUARIO);
  const corpo = await mockLocais(page);
  await page.route(`${API}/api/locais/buscar**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { nome: 'Praia de Aruana', cidade: 'Aracaju', estado: 'SE', latitude: '-10.95', longitude: '-37.05' }
      ])
    })
  );
  await mockPagina(page, '/hoje');
  await page.goto('/boas-vindas');

  await page.getByLabel('Onde você pesca?').fill('Aruana');
  await page.getByRole('button', { name: 'Praia de Aruana · SE' }).click();

  await expect(page).toHaveURL(/\/hoje$/);
  expect(corpo()?.latitude).toBe(-10.95);
  expect(corpo()?.longitude).toBe(-37.05);
  expect(corpo()?.padrao).toBe(true);
});

test('menos de dois caracteres não busca', async ({ page }) => {
  await mockMe(page, USUARIO);
  let chamadas = 0;
  await page.route(`${API}/api/locais/buscar**`, (route) => {
    chamadas += 1;
    return route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
  await page.goto('/boas-vindas');
  await page.getByLabel('Onde você pesca?').fill('a');
  await page.waitForTimeout(500);
  expect(chamadas).toBe(0);
});

test('busca vazia tem mensagem', async ({ page }) => {
  await mockMe(page, USUARIO);
  await page.route(`${API}/api/locais/buscar**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  );
  await page.goto('/boas-vindas');
  await page.getByLabel('Onde você pesca?').fill('xyz');
  await expect(page.locator('#boas-local-status')).toHaveText(
    'Nenhum lugar encontrado. Tente o nome da cidade.'
  );
});

test('erro de busca tem mensagem', async ({ page }) => {
  await mockMe(page, USUARIO);
  await page.route(`${API}/api/locais/buscar**`, (route) =>
    route.fulfill({ status: 500, contentType: 'application/json', body: '{}' })
  );
  await page.goto('/boas-vindas');
  await page.getByLabel('Onde você pesca?').fill('aracaju');
  await expect(page.locator('#boas-local-status')).toHaveText(
    'Não foi possível buscar agora. Tente de novo.'
  );
});
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm run test:e2e -- tests/boas-vindas.spec.ts`
Expected: FAIL — não há botão “Aracaju” nem POST.

- [ ] **Step 3: Renderizar as três sugestões**

No frontmatter da página, definir as três opções e renderizar `<button class="boas-sugestao" type="button" data-nome=... data-cidade=... data-estado=... data-latitude=... data-longitude=... data-ambiente=...>`. Valores: Aracaju/SE, mar, `-10.911`/`-37.071`; Ilhabela/SP, mar, `-23.778`/`-45.358`; Campinas/SP, represa, `-22.905`/`-47.060`.

- [ ] **Step 4: Implementar `destino`, `salvarLocal` e `buscarLocal`**

```ts
function destino(): string {
  try {
    const salvo = sessionStorage.getItem('fisga:retorno');
    if (salvo !== null) sessionStorage.removeItem('fisga:retorno');
    return destinoSeguro(salvo, '/hoje');
  } catch {
    return '/hoje';
  }
}

let salvando = false;

async function salvarLocal(local: LocalEscolhido): Promise<void> {
  if (salvando) return;
  salvando = true;
  if (status) status.textContent = 'Abrindo seu dia…';
  // POST /api/locais com padrao: true; 401 -> /entrar; erro -> status + salvando = false
  // sucesso -> window.location.assign(destino())
}

async function buscarLocal(termo: string): Promise<void> {
  // termo.trim().length < 2 -> esconder resultados e limpar status
  // GET /api/locais/buscar?q=... ; montar <li><button> com textContent
  // vazio -> status da spec; erro -> status da spec
}
```

Sugestões: `salvarLocal` com os `data-*` convertidos por `Number(...)`. Busca: debounce de 320 ms no `input`. Enquanto salva, `salvando = true` bloqueia chamadas seguintes.

- [ ] **Step 5: Rodar e ver passar**

Run: `npm run test:e2e -- tests/boas-vindas.spec.ts`
Expected: 9 passed.

- [ ] **Step 6: Commit**

```bash
git add Projetos/fisga/web/src/pages/boas-vindas.astro Projetos/fisga/web/tests/boas-vindas.spec.ts
git commit -m "feat(fisga): escolher local na boas-vindas"
```

---

### Task 3: Geolocalização, pular e proteção contra duplo envio

**Files:**
- Modify: `Projetos/fisga/web/src/pages/boas-vindas.astro`
- Test: `Projetos/fisga/web/tests/boas-vindas.spec.ts`

**Interfaces:**
- Consumes: `salvarLocal`, `destino`, `#boas-pular`, `#boas-usar-localizacao`.
- Produces: comportamento final da tela; nenhum símbolo novo.

- [ ] **Step 1: Escrever os testes que falham**

```ts
test('geolocalização aceita salva e navega', async ({ page }) => {
  await mockMe(page, USUARIO);
  const corpo = await mockLocais(page);
  await mockPagina(page, '/hoje');
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: {
        getCurrentPosition: (ok: PositionCallback) =>
          ok({ coords: { latitude: -10.911234, longitude: -37.071234 } } as GeolocationPosition)
      }
    });
  });
  await page.goto('/boas-vindas');
  await page.locator('#boas-usar-localizacao').click();
  await expect(page).toHaveURL(/\/hoje$/);
  expect(corpo()).toEqual({
    nome: 'Minha localização',
    latitude: -10.91123,
    longitude: -37.07123,
    ambiente: 'Outro',
    padrao: true
  });
});

test('geolocalização negada mostra instrução e restaura o botão', async ({ page }) => {
  await mockMe(page, USUARIO);
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: { getCurrentPosition: (_ok: PositionCallback, erro: PositionErrorCallback) => erro(new Error('denied') as GeolocationPositionError) }
    });
  });
  await page.goto('/boas-vindas');
  await page.locator('#boas-usar-localizacao').click();
  await expect(page.locator('#boas-local-status')).toHaveText(
    'Não conseguimos usar sua localização. Busque pelo nome.'
  );
  await expect(page.locator('#boas-usar-localizacao')).toBeEnabled();
  await expect(page.locator('#boas-usar-localizacao')).toHaveText('Usar minha localização');
});

test('Agora não respeita o destino interno e não salva', async ({ page }) => {
  await mockMe(page, USUARIO);
  let posts = 0;
  await page.route(`${API}/api/locais`, (route) => {
    if (route.request().method() === 'POST') posts += 1;
    return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
  await mockPagina(page, '/diario');
  await page.goto('/boas-vindas');
  await page.evaluate(() => sessionStorage.setItem('fisga:retorno', '/diario'));
  await page.locator('#boas-pular').click();
  await expect(page).toHaveURL(/\/diario$/);
  expect(posts).toBe(0);
});

test('destino externo é ignorado', async ({ page }) => {
  await mockMe(page, USUARIO);
  await mockPagina(page, '/hoje');
  await page.goto('/boas-vindas');
  await page.evaluate(() => sessionStorage.setItem('fisga:retorno', 'https://evil.example'));
  await page.locator('#boas-pular').click();
  await expect(page).toHaveURL(/\/hoje$/);
});

test('duas escolhas rápidas geram um único POST', async ({ page }) => {
  await mockMe(page, USUARIO);
  let posts = 0;
  await page.route(`${API}/api/locais`, async (route) => {
    if (route.request().method() === 'POST') {
      posts += 1;
      await new Promise((resolve) => setTimeout(resolve, 300));
      return route.fulfill({ status: 201, contentType: 'application/json', body: '{}' });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
  await mockPagina(page, '/hoje');
  await page.goto('/boas-vindas');
  await page.getByRole('button', { name: 'Aracaju' }).click();
  await page.getByRole('button', { name: 'Ilhabela' }).click({ force: true });
  await expect(page).toHaveURL(/\/hoje$/);
  expect(posts).toBe(1);
});
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm run test:e2e -- tests/boas-vindas.spec.ts`
Expected: FAIL nos 5 novos casos.

- [ ] **Step 3: Implementar geolocalização e pular**

No clique de `#boas-usar-localizacao`: se `navigator.geolocation` não existir, status “Seu navegador não oferece localização.”; senão, desabilitar, texto “Localizando...”, `getCurrentPosition` com `{ enableHighAccuracy: false, timeout: 12000, maximumAge: 300000 }`; sucesso → `salvarLocal({ nome: 'Minha localização', latitude: Number(lat.toFixed(5)), longitude: Number(lon.toFixed(5)), ambiente: 'Outro' })`; erro → restaurar botão e status da spec.

Em `#boas-pular`: `window.location.assign(destino())`. O guard `salvando` de `salvarLocal` já impede o segundo POST.

- [ ] **Step 4: Rodar e ver passar**

Run: `npm run test:e2e -- tests/boas-vindas.spec.ts`
Expected: 14 passed.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/web/src/pages/boas-vindas.astro Projetos/fisga/web/tests/boas-vindas.spec.ts
git commit -m "feat(fisga): geolocalização e pular na boas-vindas"
```

---

### Task 4: Verificação e acabamento visual

> **Nota de execução:** o input ficou com 48 px e o H1 com `clamp(2.4rem, 5.4vw, 3.5rem)`,
> seguindo o artefato do OpenDesign; a suíte final da tela tem 25 testes. A revisão de código
> acrescentou os estados de falha (save 500/rede/401), a ordem de respostas da busca, o anúncio
> de resultados e os alvos de 44 px.

**Files:**
- Modify: `Projetos/fisga/web/src/pages/boas-vindas.astro` (só CSS, se necessário)

**Interfaces:**
- Consumes: artefato do OpenDesign em `Fisga Boas-vindas` e a tela implementada.
- Produces: nenhum símbolo novo.

- [ ] **Step 1: Rodar a verificação estática**

Run: `npm run check`
Expected: `0 errors`, `0 warnings`, `0 hints`.

- [ ] **Step 2: Rodar a suíte mockada inteira**

Run: `npm run test:e2e`
Expected: todos os testes passam, incluindo `tests/auth.spec.ts` e `tests/boas-vindas.spec.ts`.

- [ ] **Step 3: Rodar o build**

Run: `npm run build`
Expected: build conclui sem erro.

- [ ] **Step 4: Comparar com o artefato do OpenDesign**

Abrir o preview do artefato e a tela no dev server em 1440×900 e 390×844. Conferir: título na mesma proporção, busca como elemento dominante, chips em uma linha no desktop e quebrando no mobile, linha de privacidade visível sem scroll no mobile. Ajustar somente espaçamento, tamanho de tipo e largura — estrutura e copy não mudam.

- [ ] **Step 5: Commit final**

```bash
git add Projetos/fisga/web/src/pages/boas-vindas.astro
git commit -m "style(fisga): acabamento da boas-vindas"
```
