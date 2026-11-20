# Aviso de defeso por captura — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Levar o aviso de defeso do painel da etapa 2 para cada linha de captura da etapa 3, com estado independente por captura.

**Architecture:** O markup do aviso sai da etapa 2 e passa a nascer em `criarLinhaCaptura()`, dentro de `.linha-captura`, com `grid-column: 1 / -1`. `atualizarDefeso()` deixa de consultar só a primeira captura e itera `#lista-capturas .linha-captura`, preenchendo o aviso da linha quando espécie e UF existem. Nenhuma mudança de API, contrato, banco ou configuração.

**Tech Stack:** Astro SSR, TypeScript no `<script>` do `.astro`, CSS no bloco `<style>` da página, Playwright E2E com API mockada.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-20-defeso-por-captura-design.md`

**Estado de entrada:** issue #57 promovida e reivindicada; worktree `issue-57-defeso-por-captura`; `main` em `d91cb7e` (PR #56 merged).

---

## Task 1: E2E que falha (red)

**Files:**
- Create: `Projetos/fisga/web/tests/registrar-defeso.spec.ts`

- [ ] **Step 1: Criar o spec com o fluxo até a etapa 3 e quatro casos**

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

async function abrirEtapa3(page: Page) {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#estado').fill('SP');
  await page.locator('#data').fill('2026-11-20');
  await page.getByRole('button', { name: /3 As capturas/ }).click();
  await expect(page.locator('[data-etapa="3"]')).toBeVisible();
}

test('cada captura exibe o próprio aviso de defeso com link para a UF', async ({ page }) => {
  await abrirEtapa3(page);
  const linha = page.locator('.linha-captura').first();
  await linha.locator('.captura-especie').fill('Robalo');

  const alerta = linha.locator('.defeso-alerta');
  await expect(alerta).toBeVisible();
  await expect(alerta).toContainText('defeso de Robalo em SP');
  await expect(alerta.getByRole('link', { name: 'Abrir órgão oficial' }))
    .toHaveAttribute('href', '/ferramentas/defeso?uf=SP');
});

test('duas capturas de espécies diferentes mantêm avisos independentes', async ({ page }) => {
  await abrirEtapa3(page);
  const linhas = page.locator('.linha-captura');
  await linhas.first().locator('.captura-especie').fill('Robalo');

  await page.locator('#adicionar-captura').click();
  await expect(linhas).toHaveCount(2);
  await linhas.nth(1).locator('.captura-especie').fill('Tainha');

  await expect(linhas.nth(0).locator('.defeso-alerta')).toContainText('defeso de Robalo em SP');
  await expect(linhas.nth(1).locator('.defeso-alerta')).toContainText('defeso de Tainha em SP');

  await linhas.nth(1).locator('.captura-especie').fill('Corvina');
  await expect(linhas.nth(1).locator('.defeso-alerta')).toContainText('defeso de Corvina em SP');
  await expect(linhas.nth(0).locator('.defeso-alerta')).toContainText('defeso de Robalo em SP');
});

test('sem espécie ou sem UF o aviso some e é limpo', async ({ page }) => {
  await abrirEtapa3(page);
  const linha = page.locator('.linha-captura').first();
  const alerta = linha.locator('.defeso-alerta');
  await linha.locator('.captura-especie').fill('Robalo');
  await expect(alerta).toBeVisible();

  await page.getByRole('button', { name: /1 A pescaria/ }).click();
  await page.locator('#estado').fill('');
  await page.getByRole('button', { name: /3 As capturas/ }).click();
  await expect(alerta).toBeHidden();
  await expect(alerta).toBeEmpty();

  await page.getByRole('button', { name: /1 A pescaria/ }).click();
  await page.locator('#estado').fill('SP');
  await page.getByRole('button', { name: /3 As capturas/ }).click();
  await expect(alerta).toBeVisible();
  await linha.locator('.captura-especie').fill('');
  await expect(alerta).toBeHidden();
  await expect(alerta).toBeEmpty();
});

test('o aviso não existe mais na etapa 2 e vive na linha da etapa 3', async ({ page }) => {
  await abrirEtapa3(page);
  await expect(page.locator('#defeso-alerta')).toHaveCount(0);
  await expect(page.locator('[data-etapa="2"] .defeso-alerta')).toHaveCount(0);
  await expect(page.locator('[data-etapa="3"] .defeso-alerta')).toHaveCount(1);
});
```

- [ ] **Step 2: Buildar o web e rodar só este spec**

```bash
cd Projetos/fisga/web
npm run build
npx playwright test registrar-defeso.spec.ts
```

Esperado (red): os testes 1–3 falham porque não existe `.defeso-alerta` na linha e a etapa 2 ainda tem `#defeso-alerta`; o teste 4 falha no `#defeso-alerta`.

---

## Task 2: Mover o markup e ajustar o CSS

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro` (etapa 2, `criarLinhaCaptura`, `<style>`)

- [ ] **Step 1: Remover o bloco da etapa 2**

Remover de `registrar.astro`:

```html
        <div id="defeso-alerta" class="defeso-alerta oculto" role="note"></div>
```

- [ ] **Step 2: Criar o aviso dentro da linha, antes de `.captura-fotos`**

Em `criarLinhaCaptura()`, no template `linha.innerHTML`, inserir entre o botão remover e `.captura-fotos`:

```html
        <div class="defeso-alerta oculto" role="note"></div>
```

- [ ] **Step 3: Dar id único ao aviso e ligar o campo de espécie**

Logo depois do `linha.innerHTML = \`...\`;`, antes do preenchimento dos valores:

```ts
      const alertaDefeso = linha.querySelector<HTMLElement>('.defeso-alerta');
      if (alertaDefeso) alertaDefeso.id = `defeso-${++sequenciaDefeso}`;
```

Declarar o contador junto das demais variáveis de estado (perto de `let sequenciaUploads = 0;`):

```ts
    let sequenciaDefeso = 0;
```

- [ ] **Step 4: Ajustar o CSS**

No bloco `<style>` de `registrar.astro`, em `.defeso-alerta`, trocar `margin-top: 12px;` por:

```css
    grid-column: 1 / -1;
    margin-top: 0;
```

No `@media (max-width: 760px)` existente no fim do `<style>`, adicionar:

```css
    .defeso-alerta a {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
    }
```

- [ ] **Step 5: Conferir que nada mais usa `#defeso-alerta`**

```bash
grep -rn "defeso-alerta" Projetos/fisga/web/src
```

Esperado: só `criarLinhaCaptura`, o CSS e — depois da Task 3 — `atualizarDefeso`.

---

## Task 3: Atualizar todas as capturas

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro:975`

- [ ] **Step 1: Substituir `atualizarDefeso()` inteira**

```ts
    function atualizarDefeso() {
      const estado = document.getElementById('estado').value.trim().toUpperCase();
      const ufValida = estado.length === 2;

      for (const linha of listaCapturas.querySelectorAll('.linha-captura')) {
        const alerta = linha.querySelector('.defeso-alerta');
        const especieInput = linha.querySelector('.captura-especie');
        if (!alerta || !especieInput) continue;

        const especie = especieInput.value.trim();
        if (!ufValida || !especie) {
          alerta.classList.add('oculto');
          alerta.replaceChildren();
          especieInput.removeAttribute('aria-describedby');
          continue;
        }

        const texto = document.createElement('span');
        texto.textContent = `Antes de manter: confira tamanho mínimo e defeso de ${especie} em ${estado}.`;
        const link = document.createElement('a');
        link.href = `/ferramentas/defeso?uf=${encodeURIComponent(estado)}`;
        link.textContent = 'Abrir órgão oficial';
        alerta.replaceChildren(texto, link);
        alerta.classList.remove('oculto');
        if (alerta.id) especieInput.setAttribute('aria-describedby', alerta.id);
      }
    }
```

- [ ] **Step 2: Garantir atualização ao criar/editar linhas**

`atualizarDefeso()` já é chamada em `formulario` (`input`/`change`) e em `preencher()` (linha 1216). Confirmar que `criarLinhaCaptura()` não precisa de chamada própria — a linha nasce vazia e o primeiro `input` na espécie dispara a atualização. Não adicionar chamada em `iniciarFormularioVazio()`.

- [ ] **Step 3: Rodar os testes**

```bash
cd Projetos/fisga/web
npm run build
npx playwright test registrar-defeso.spec.ts registrar-etapas-foco.spec.ts
```

Esperado (green): os quatro casos novos passam e os testes de foco entre etapas continuam passando.

---

## Task 4: Documentação e verificação

**Files:**
- Modify: `Projetos/fisga/docs/INDEX.md`
- Already created: `docs/superpowers/specs/2026-11-20-defeso-por-captura-design.md`

- [ ] **Step 1: Indexar spec e plano**

Em `docs/INDEX.md`, seção `## Planejamento`, após a linha do plano de upload de fotos:

```md
- [Spec — Aviso de defeso por captura](superpowers/specs/2026-11-20-defeso-por-captura-design.md)
- [Plano — Aviso de defeso por captura](superpowers/plans/2026-11-20-defeso-por-captura.md)
```

- [ ] **Step 2: Lint de Markdown**

```bash
cd Projetos/fisga
npx --yes markdownlint-cli2@0.24.1
```

- [ ] **Step 3: Verificação completa**

```bash
cd Projetos/fisga
export PATH="$HOME/.dotnet:$PATH"
dotnet restore Fisga.slnx --locked-mode
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build
cd web
npm run check
npm run build
npx playwright test
```

Esperado: tudo verde. Se `dotnet` não for afetado, ainda assim rodar para provar que o PR não quebrou nada.

- [ ] **Step 4: Commit e PR**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro \
        Projetos/fisga/web/tests/registrar-defeso.spec.ts \
        Projetos/fisga/docs/superpowers/specs/2026-11-20-defeso-por-captura-design.md \
        Projetos/fisga/docs/superpowers/plans/2026-11-20-defeso-por-captura.md \
        Projetos/fisga/docs/INDEX.md
git commit -m "feat(registrar): aviso de defeso por captura na etapa 3"
```

Abrir PR com `Closes #57`, corpo curto com o antes/depois e a evidência dos testes; depois `gate`, threads, merge e deploy.

---

## Critérios de aceite (da issue #57)

- [ ] Cada captura com espécie e UF mostra o próprio aviso na etapa 3.
- [ ] Trocar a espécie da segunda linha atualiza só o aviso dela.
- [ ] Duas espécies diferentes mostram dois avisos independentes.
- [ ] Sem espécie ou sem UF o aviso some e é limpo.
- [ ] O link aponta para `/ferramentas/defeso?uf=<UF>`.
- [ ] Nenhum aviso de defeso permanece na etapa 2.
