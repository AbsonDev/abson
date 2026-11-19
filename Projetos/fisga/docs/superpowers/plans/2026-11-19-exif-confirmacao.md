# Confirmação explícita dos dados da foto — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nenhum campo de `/registrar` pode ser preenchido por EXIF/GPS antes de "Aplicar", e o GPS nunca escreve o local exato.

**Architecture:** `aplicarExif()` passa a só calcular sugestões em memória e exibir um aviso inline com "Aplicar"/"Ignorar". Um único booleano de sessão registra a decisão; a escrita nos campos acontece apenas em `confirmarExif()`, respeitando os valores já digitados. A UI segue o artefato do OpenDesign `fisga-produto/registrar/index.html`.

**Tech Stack:** Astro SSR + TypeScript no cliente, `exifr@^7.1.3`, Playwright E2E mockado com `page.route`.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-19-exif-confirmacao-design.md`

## Global Constraints

- Copy em pt-BR, direta e calma; sem termos de marketing. Texto exato do aviso:
  "Encontramos data, horário e cidade na foto. Só preenchemos campos que estão vazios — o local exato nunca vem da foto." Valores lidos em uma linha discreta, ex.: "18/11/2026, 06:42 · Ilhabela, SP". Confirmação pós-Aplicar: "Campos vazios preenchidos pela foto." com ícone de check e palavra, nunca só cor.
- Nenhuma mudança de API, contrato OpenAPI ou projeção `LocalPublico`.
- Campo `#local` nunca recebe valor do GPS, nem quando vazio.
- Apenas a primeira foto com dados úteis é considerada; decisão vale pelo resto da sessão.
- Edição (`?editar=`) e restauração de rascunho não disparam aviso.
- Alvos de toque de 44 px e anel de foco visível; aviso é `role="status"`, não modal.
- Validação: `cd Projetos/fisga/web && npm ci && npm run check && npm run build && npm run test:e2e`.
- Lint de Markdown na raiz: `npx --yes markdownlint-cli2@0.24.1`.

## Review Focus

| Risco | Teste que o fixa |
| --- | --- |
| Foto sem EXIF/GPS dispara aviso vazio | Task 1, teste "foto sem dados não abre aviso e mantém o fluxo" |
| Falha no `/api/locais/reverso` derruba a sugestão de data | Task 2, teste "reverso indisponível ainda mostra data e aplica só o que veio da foto" |
| Pescador digita data/período enquanto o upload corre e"Aplicar" sobrescreve | Task 2, teste "Aplicar não sobrescreve campo digitado durante o upload" |
| Segunda foto com dados reaparece depois de "Ignorar" | Task 3, teste "Ignorar é definitivo na sessão" |
| Restauração de rascunho dispara aviso e perde valores | Task 3, teste "rascunho restaurado não abre aviso" |

---

### Task 1: Aviso aparece antes de qualquer escrita

**Files:**
- Create: `web/tests/fixtures/foto-exif.ts`
- Modify: `web/src/pages/registrar.astro:60-74` (markup do aviso)
- Modify: `web/src/pages/registrar.astro:720-772` (`aplicarExif`)
- Test: `web/tests/registrar-exif.spec.ts`

**Interfaces:**
- Consumes: `#exif-aviso`, `#exif-texto`, `aplicarExif(arquivo)` existentes; `mockAnonimo` de `registrar-anonimo.spec.ts` (copiar o padrão, não importar).
- Produces: `#exif-aplicar`, `#exif-ignorar`, `#exif-estado` no DOM; `exifSugestoes` (objeto com `data`, `periodo`, `cidade`, `estado`, `partes` ou `null`) e `decisaoExifTomada: boolean` no escopo do módulo; `mostrarAvisoExif(sugestoes)`.

- [ ] **Step 1: Criar o fixture de foto com EXIF**

Gere um JPEG mínimo (2×2) com `DateTimeOriginal = 2026:11:18 06:42:00` e GPS Sul/Oeste usando Pillow + piexif, e salve o base64 (≈1,1 kB) como `FOTO_COM_EXIF_BASE64` em `web/tests/fixtures/foto-exif.ts`. Confira com `node -e` usando a `exifr` instalada: `parse` devolve `DateTimeOriginal` e `gps` devolve latitude/longitude. Exporte também `fotoSemExif()` (JPEG 2×2 sem APP1) para o caso negativo.

- [ ] **Step 2: Escrever os testes que falham**

Em `web/tests/registrar-exif.spec.ts`, com `mockAnonimo`, `mockCondicoes` e `page.route` em `/api/locais/reverso` devolvendo `{cidade:'Ilhabela', estado:'SP'}` com os headers CORS do padrão:

```ts
test('foto com EXIF mostra aviso com Aplicar e Ignorar sem mudar nenhum campo', async ({ page }) => {
  await mockAnonimo(page); await mockCondicoes(page);
  await page.route(`${API}/api/locais/reverso**`, (route) => route.fulfill({ status: 200, headers: CORS, contentType: 'application/json', body: JSON.stringify({ cidade: 'Ilhabela', estado: 'SP' }) }));
  await page.route(`${API}/api/midia**`, (route) => route.fulfill({ status: 201, headers: CORS, contentType: 'application/json', body: '{}' }));
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#data').fill('2026-11-19');
  await page.locator('#periodo').fill('Manha');
  await page.locator('#local').fill('Praia do Meio');
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'foto.jpg', mimeType: 'image/jpeg', buffer: Buffer.from(FOTO_COM_EXIF_BASE64, 'base64') });
  await expect(page.locator('#exif-aviso')).toBeVisible();
  await expect(page.locator('#exif-texto')).toContainText('Ilhabela');
  await expect(page.getByRole('button', { name: /Aplicar nos campos vazios/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ignorar' })).toBeVisible();
  await expect(page.locator('#data')).toHaveValue('2026-11-19');
  await expect(page.locator('#periodo')).toHaveValue('Manha');
  await expect(page.locator('#local')).toHaveValue('Praia do Meio');
});

test('foto sem dados não abre aviso e mantém o fluxo', async ({ page }) => {
  /* mesmo setup com fotoSemExif(); #exif-aviso permanece .oculto */
  await expect(page.locator('#exif-aviso')).toBeHidden();
  await expect(page.locator('input[type="file"]').first()).toBeEnabled();
});
```

- [ ] **Step 3: Rodar e confirmar que falham**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-exif.spec.ts`
Expected: FAIL — `#exif-aplicar`/`#exif-ignorar` não existem e os campos já mudam antes da decisão.

- [ ] **Step 4: Implementar cálculo sem escrita e markup do aviso**

Em `aplicarExif()`: ler EXIF/GPS como hoje, montar `exifSugestoes` com `data` (`YYYY-MM-DD`), `periodo` (`periodoDaHora`), `cidade`, `estado` e `partes` para o texto, **sem tocar em nenhum `input`**. Se não houver dados úteis, sair sem aviso. Chamar `mostrarAvisoExif()` para preencher `#exif-texto` com a cópia exata dos Global Constraints e `#exif-valores` com os valores lidos, exibindo `#exif-aviso`. Seguir as classes do artefato (`.photo-meta`, `.photo-meta-copy`, `.photo-meta-values`, `.photo-meta-actions`) e o layout: ações lado a lado no desktop e empilhadas no mobile. Trocar o botão "Ocultar" por `#exif-aplicar` ("Aplicar nos campos vazios", primário) e `#exif-ignorar` ("Ignorar", secundário), ambos com 44 px de altura. Renomear `exifAplicado` para `decisaoExifTomada` em todas as ocorrências (`:444`, `:659`, `:721`, `:1199`, `:1461`, `:1510`), mantendo `true` nos caminhos de edição/rascunho.

- [ ] **Step 5: Rodar e confirmar que passam**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-exif.spec.ts`
Expected: PASS nos dois testes.

- [ ] **Step 6: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-exif.spec.ts Projetos/fisga/web/tests/fixtures/foto-exif.ts
git commit -m "test(web): travar aviso de EXIF antes de qualquer escrita"
```

---

### Task 2: Aplicar preenche só campos vazios

**Files:**
- Modify: `web/src/pages/registrar.astro` (`confirmarExif`, handlers)
- Test: `web/tests/registrar-exif.spec.ts`

**Interfaces:**
- Consumes: `exifSugestoes`, `decisaoExifTomada`, `#exif-aplicar`, `#exif-estado` da Task 1.
- Produces: `confirmarExif(): void`; `#exif-estado` com a confirmação pós-aplicação.

- [ ] **Step 1: Escrever os testes que falham**

```ts
test('Aplicar preenche data, período, cidade e estado vazios, nunca o local', async ({ page }) => {
  /* setup da foto com EXIF, sem digitar nada */
  await page.getByRole('button', { name: /Aplicar nos campos vazios/ }).click();
  await expect(page.locator('#data')).toHaveValue('2026-11-18');
  await expect(page.locator('#periodo')).toHaveValue('Manha');
  await expect(page.locator('#cidade')).toHaveValue('Ilhabela');
  await expect(page.locator('#estado')).toHaveValue('SP');
  await expect(page.locator('#local')).toHaveValue('');
  await expect(page.locator('#exif-estado')).toContainText('Campos vazios preenchidos pela foto.');
});

test('Aplicar não sobrescreve campo digitado durante o upload', async ({ page }) => {
  /* digitar data 2026-11-19 e período Noite antes do clique */
  await page.getByRole('button', { name: /Aplicar nos campos vazios/ }).click();
  await expect(page.locator('#data')).toHaveValue('2026-11-19');
  await expect(page.locator('#periodo')).toHaveValue('Noite');
});

test('reverso indisponível ainda mostra data e aplica só o que veio da foto', async ({ page }) => {
  await page.route(`${API}/api/locais/reverso**`, (route) => route.abort());
  await expect(page.locator('#exif-texto')).toContainText('18/11/2026');
  await page.getByRole('button', { name: /Aplicar nos campos vazios/ }).click();
  await expect(page.locator('#data')).toHaveValue('2026-11-18');
  await expect(page.locator('#cidade')).toHaveValue('');
});
```

- [ ] **Step 2: Rodar e confirmar que falham**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-exif.spec.ts`
Expected: FAIL — `confirmarExif` inexistente; `#exif-estado` vazio.

- [ ] **Step 3: Implementar `confirmarExif()`**

Para cada chave em `exifSugestoes`, escrever no `input` correspondente **somente se `input.value` estiver vazio**. Nunca escrever em `#local`. Marcar `decisaoExifTomada = true`, limpar `exifSugestoes`, esconder `#exif-aviso`, esconder `#exif-aplicar`/`#exif-ignorar` e mostrar `#exif-estado` com `role="status"`, ícone de check e o texto exato "Campos vazios preenchidos pela foto." (classe `.photo-meta-done` do artefato). O foco volta ao primeiro campo após a decisão, como no artefato. O `fetch` do reverso já usa `.catch()` implícito do bloco `try`; garantir que uma falha de rede não impeça a sugestão de data de chegar ao aviso.

- [ ] **Step 4: Rodar e confirmar que passam**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-exif.spec.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-exif.spec.ts
git commit -m "feat(web): aplicar dados da foto só nos campos vazios"
```

---

### Task 3: Ignorar é definitivo e rascunho não dispara aviso

**Files:**
- Modify: `web/src/pages/registrar.astro` (`ignorarExif`, guard de `aplicarExif`)
- Test: `web/tests/registrar-exif.spec.ts`

**Interfaces:**
- Consumes: `decisaoExifTomada`, `exifSugestoes`, `#exif-ignorar` das tasks anteriores.
- Produces: `ignorarExif(): void`.

- [ ] **Step 1: Escrever os testes que falham**

```ts
test('Ignorar não muda campo e não reaparece para a segunda foto', async ({ page }) => {
  /* primeira foto com EXIF */
  await page.getByRole('button', { name: 'Ignorar' }).click();
  await expect(page.locator('#data')).toHaveValue('');
  await expect(page.locator('#exif-aviso')).toBeHidden();
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'foto2.jpg', mimeType: 'image/jpeg', buffer: Buffer.from(FOTO_COM_EXIF_BASE64, 'base64') });
  await expect(page.locator('#exif-aviso')).toBeHidden();
});

test('rascunho restaurado não abre aviso e preserva os valores', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', data: '2026-11-19', periodo: 'Tarde', capturas: [] }
  })));
  await page.goto(`${ORIGEM}/registrar`);
  await expect(page.locator('#exif-aviso')).toBeHidden();
  await expect(page.locator('#data')).toHaveValue('2026-11-19');
});

test('edição existente não abre aviso', async ({ page }) => {
  /* page.route em /api/pescarias/** devolvendo uma entrada mínima; ?editar=id */
  await expect(page.locator('#exif-aviso')).toBeHidden();
});
```

- [ ] **Step 2: Rodar e confirmar que falham**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-exif.spec.ts`
Expected: FAIL — `ignorarExif` inexistente; aviso reaparece.

- [ ] **Step 3: Implementar `ignorarExif()` e o guard**

`ignorarExif()` limpa `exifSugestoes`, marca `decisaoExifTomada = true` e esconde `#exif-aviso` sem tocar em nenhum campo. `aplicarExif()` começa com `if (decisaoExifTomada) return;`. Conferir que os três caminhos que já marcavam a decisão continuam marcando: carregamento de `?editar=`, hidratação síncrona do rascunho e reidratação assíncrona do rascunho.

- [ ] **Step 4: Rodar e confirmar que passam**

Run: `cd Projetos/fisga/web && npm run test:e2e -- registrar-exif.spec.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-exif.spec.ts
git commit -m "fix(web): decisão sobre dados da foto vale para a sessão"
```

---

### Task 4: Documentação e gate completo

**Files:**
- Modify: `Projetos/fisga/docs/INDEX.md` (seção Planejamento)
- Modify: `Projetos/fisga/docs/superpowers/specs/2026-11-19-exif-confirmacao-design.md` (status aprovado)

**Interfaces:**
- Consumes: comportamento entregue nas tasks 1–3.

- [ ] **Step 1: Indexar a spec**

Adicionar em `## Planejamento` de `docs/INDEX.md`: `- [Spec — Confirmação explícita dos dados da foto](superpowers/specs/2026-11-19-exif-confirmacao-design.md)`. Trocar `**Status:** aguardando aprovação` por `**Status:** aprovado pelo usuário em 19/11/2026`.

- [ ] **Step 2: Rodar a validação completa**

```bash
cd Projetos/fisga/web && npm ci && npm run check && npm run build && npm run test:e2e
cd ../.. && npx --yes markdownlint-cli2@0.24.1
```

Expected: `astro check` 0 erros, build ok, suíte E2E verde (incluindo os novos testes), markdownlint 0 issues.

- [ ] **Step 3: Commit**

```bash
git add Projetos/fisga/docs/INDEX.md Projetos/fisga/docs/superpowers/specs/2026-11-19-exif-confirmacao-design.md
git commit -m "docs(fisga): indexar spec de confirmação de EXIF"
```
