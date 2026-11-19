# Foco entre etapas do `/registrar` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer o foco ir para o `<h2>` do painel visível ao trocar de etapa no `/registrar`, com nome acessível `Etapa N — <título>`.

**Architecture:** `mostrarEtapa()` passa a focar o título do painel ativo com `preventScroll`; os quatro títulos ganham `tabindex="-1"` e um prefixo oculto estático. O scroll suave existente e a precedência do campo inválido na validação permanecem. Nenhuma mudança de API, contrato ou configuração.

**Tech Stack:** Astro SSR, TypeScript no `<script>` do `.astro`, CSS global, Playwright E2E com API mockada.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-19-foco-etapas-design.md`

**Estado de entrada:** o draft foi promovido à issue #53 e o claim local (`issue-53-foco-etapas`) já foi feito pelo controlador antes destas tasks.

## Global Constraints

- Copy em pt-BR; prefixo exatamente `Etapa N — ` (com espaço depois do travessão).
- Sem mudança de contrato, OpenAPI, banco, configuração ou workflow.
- Documentação de comportamento atualizada no mesmo PR e indexada em `docs/INDEX.md`.
- Nenhum segredo, token ou DSN no diff.
- Commits pequenos e convencionais.
- UI e copy em português do Brasil.

## Review Focus

- Validação com `#local`/`#data` vazio: o campo inválido deve vencer o foco do título — teste em Task 2.
- Carga inicial não deve roubar foco (`document.activeElement` é `BODY`) — teste em Task 2.
- Chamadas repetidas de `mostrarEtapa()` não devem duplicar o prefixo — decorator estático no markup; conferir no review de Task 3.
- `aria-labelledby` das `<section>` continua resolvendo para o título com o prefixo — nomes acessíveis assertados em Task 2.
- Scroll suave existente não deve ser alterado pelo foco — `preventScroll: true`; conferir no review de Task 3.

---

### Task 1: Indexar a spec na documentação

**Files:**
- Modify: `Projetos/fisga/docs/INDEX.md` (seção `## Planejamento`)

**Interfaces:**
- Consumes: `Projetos/fisga/docs/superpowers/specs/2026-11-19-foco-etapas-design.md` (já escrita pelo controlador).
- Produces: spec alcançável a partir da porta de entrada da documentação.

- [ ] **Step 1: adicionar a linha da spec em `## Planejamento`**

Ao lado das specs de 19/11/2026, inserir:

```markdown
- [Spec — Foco entre etapas do registrar](superpowers/specs/2026-11-19-foco-etapas-design.md)
```

- [ ] **Step 2: verificar o link**

Run: `test -f Projetos/fisga/docs/superpowers/specs/2026-11-19-foco-etapas-design.md && echo OK`
Expected: `OK`

- [ ] **Step 3: commit**

```bash
git add Projetos/fisga/docs/INDEX.md
git commit -m "docs(fisga): indexar spec de foco entre etapas"
```

### Task 2: Teste E2E vermelho do foco entre etapas

**Files:**
- Create: `Projetos/fisga/web/tests/registrar-etapas-foco.spec.ts`
- Test: o próprio arquivo

**Interfaces:**
- Consumes: padrão de mock de `registrar-anonimo.spec.ts` (`PORTA`, `ORIGEM`, `API`, `CORS`, `mockAnonimo`).
- Produces: cinco testes que falham até a Task 3 implementar o foco.

- [ ] **Step 1: escrever o arquivo de teste completo**

```ts
import { test, expect, type Page } from '@playwright/test';

const PORTA = Number(process.env.PORT ?? 4323);
const ORIGEM = `http://localhost:${PORTA}`;
const API = 'http://localhost:5184';
const CORS = {
  'access-control-allow-origin': ORIGEM,
  'access-control-allow-credentials': 'true'
};

async function mockAnonimo(page: Page) {
  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({
        autenticado: false,
        usuario: null,
        consentimentoPendente: false,
        googleConectado: false,
        temSenha: false
      })
    })
  );
}

async function abrirEtapa2(page: Page) {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /2 As condições/ }).click();
}

test('avançar para a etapa 2 foca o título com o prefixo da etapa', async ({ page }) => {
  await abrirEtapa2(page);
  await expect(page.locator('[data-etapa="2"]')).toBeVisible();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-condicoes');
  await expect(page.locator('#titulo-condicoes')).toHaveAccessibleName('Etapa 2 — As condições');
});

test('voltar foca o título da etapa anterior', async ({ page }) => {
  await abrirEtapa2(page);
  await page.locator('[data-etapa="2"] [data-voltar]').click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-pescaria');
  await expect(page.locator('#titulo-pescaria')).toHaveAccessibleName('Etapa 1 — A pescaria');
});

test('clique na navegação foca o título de cada etapa', async ({ page }) => {
  await abrirEtapa2(page);
  await page.getByRole('button', { name: /3 As capturas/ }).click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-capturas');
  await expect(page.locator('#titulo-capturas')).toHaveAccessibleName('Etapa 3 — As capturas');
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-revisar');
  await expect(page.locator('#titulo-revisar')).toHaveAccessibleName('Etapa 4 — Revisar');
});

test('validação com local vazio mantém o foco no campo', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('');
  await page.getByRole('button', { name: /2 As condições/ }).click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('local');
});

test('carga inicial não rouba foco', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  expect(await page.evaluate(() => document.activeElement?.tagName)).toBe('BODY');
});
```

- [ ] **Step 2: rodar o teste e confirmar que falha**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-etapas-foco.spec.ts`
Expected: FAIL — ao menos os testes de avançar/voltar/navegação falham com `activeElement.id` vazio (`BODY`); o de validação e o de carga inicial podem passar já.

- [ ] **Step 3: commit do teste**

```bash
git add Projetos/fisga/web/tests/registrar-etapas-foco.spec.ts
git commit -m "test(web): foco no título ao trocar de etapa no registrar"
```

### Task 3: Implementar o foco no título

**Files:**
- Modify: `Projetos/fisga/web/src/styles/global.css`
- Modify: `Projetos/fisga/web/src/pages/registrar.astro` (quatro `<h2>` e `mostrarEtapa()`)

**Interfaces:**
- Consumes: testes de Task 2 (IDs `titulo-pescaria`, `titulo-condicoes`, `titulo-capturas`, `titulo-revisar`; nomes acessíveis `Etapa N — <título>`).
- Produces: `document.activeElement` é o `<h2>` do painel ativo após `mostrarEtapa()`.

- [ ] **Step 1: adicionar `.visually-hidden` em `global.css`**

Copiar a regra de `web/src/styles/auth.css:152` (1 px, `clip: rect(0,0,0,0)`, `white-space: nowrap`, `border: 0`) para `web/src/styles/global.css`, sem alterar nenhuma regra existente.

- [ ] **Step 2: marcar os quatro títulos em `registrar.astro`**

Em cada um dos `<h2>` (`titulo-pescaria`, `titulo-condicoes`, `titulo-capturas`, `titulo-revisar`), adicionar `tabindex="-1"` e, como primeiro filho, `<span class="visually-hidden">Etapa N — </span>` com N = 1, 2, 3, 4 respectivamente. Não alterar o texto visível nem os `aria-labelledby` das `<section>`.

- [ ] **Step 3: focar o título em `mostrarEtapa()`**

Logo após o loop que atualiza `aria-current` dos botões e antes de `if (etapaAtual === 4) atualizarRevisao();`, inserir:

```ts
etapas
  .find((etapa) => Number(etapa.dataset.etapa) === etapaAtual)
  ?.querySelector<HTMLElement>('.painel-cabecalho h2')
  ?.focus({ preventScroll: true });
```

Não tocar no `window.scrollTo` existente nem em `validarEtapa()`.

- [ ] **Step 4: rodar o teste e confirmar que passa**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-etapas-foco.spec.ts`
Expected: PASS — 5 testes.

- [ ] **Step 5: commit**

```bash
git add Projetos/fisga/web/src/styles/global.css Projetos/fisga/web/src/pages/registrar.astro
git commit -m "fix(web): focar o título da etapa ao trocar de passo"
```

### Task 4: Verificação completa do branch

**Files:** nenhum arquivo novo; verificação e, se necessário, ajustes de documentação.

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: evidência de que o branch está pronto para PR.

- [ ] **Step 1: suíte E2E completa**

Run: `cd Projetos/fisga/web && npm run test:e2e`
Expected: PASS (a base histórica é 150 testes; adicionar 5 → 155).

- [ ] **Step 2: typecheck e build**

Run: `cd Projetos/fisga/web && npm run check && npm run build`
Expected: 0 erros; build concluído.

- [ ] **Step 3: lint de Markdown**

Run: `npx --yes markdownlint-cli2@0.24.1`
Expected: 0 issues.

- [ ] **Step 4: conferir o diff**

Run: `git diff --stat 415e1d6..HEAD && git status --short`
Expected: apenas os arquivos das Tasks 1–3, sem `.env`, token ou `openapi.json`.
