# Registrar anônimo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer `/registrar` capturar primeiro e pedir conta só no salvamento, com rascunho local completo e condições da URL prevalecendo sobre rascunho antigo.

**Architecture:** Toda a mudança vive no `<script>` e no `<style>` de `web/src/pages/registrar.astro` (padrão existente do arquivo, sem extrair módulos novos). A inicialização passa a ter uma única ordem — ler rascunho, buscar condições, mesclar com precedência da URL, preencher respeitando campos tocados — usada tanto por visitantes anônimos quanto autenticados; a diferença fica no banner e no submit. Testes E2E mockados em Playwright pinam cada comportamento.

**Tech Stack:** Astro 7 + TypeScript (`@ts-nocheck` no script inline), Playwright 1.65, localStorage, `npm run check`, `npm run build`, `npm run test:e2e`.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-19-registrar-anonimo-design.md`

## Global Constraints

- UI e copy em **pt-BR**; sem exclamações, emoji, inglês ou dados de demonstração rotulados como reais.
- Tokens existentes apenas: papel `#F8F4EA`, superfície `#FFFDF8`, tinta `#172A2D`, petrol `#174E57`, terracota `#D9773F`, Fraunces + Inter; alvos de toque ≥ 44 px, foco com anel de 3 px, sem cor como único sinal.
- Nenhuma dependência nova; nenhuma chamada de API nova; nenhuma alteração em `docs/api/openapi.json`.
- Não tocar na distinção `fisga-public-*` / `fisga-private-*`, no precache ou na política de logout explícito/sessão expirada.
- Manter ids usados por testes/service worker: `#pescaria-form`, `#acesso-registrar`, `#condicoes-aplicadas`, `#condicoes-texto`, `#btn-salvar`, `fisga:rascunho:v1`, `fisga:retorno`.
- `?editar=<id>` continua autenticado e carrega a pescaria salva antes de rascunho/condições.
- Banner anônimo: `Você pode preencher agora. Entrar só quando quiser salvar.` + link `Entrar`.
- Indicador: `Rascunho salvo neste aparelho · HH:MM` — nunca promete nuvem/sincronização.
- Nota de foto: `A foto entra depois que você entra. O resto do registro continua aqui.`
- Painel: `Seu registro está pronto.` / `Para guardar no diário, entre com sua conta. O que você preencheu continua aqui, sem mudanças.` / `Entrar e salvar no diário` / `Continuar aqui`.
- Banner de condições: `Os valores do link vieram na frente do seu rascunho; o que você já tinha digitado continua aqui.` seguido dos valores aplicados.
- Antes de abrir PR: `npm run check`, `npm run build`, `npm run test:e2e` e `npx --yes markdownlint-cli2@0.24.1` na raiz do monorepo.

## Review Focus

- `?editar=<id>` com rascunho velho ou `lat`/`lon` na URL → deve editar a pescaria salva, sem mesclar nada por cima. Teste: Task 3, Step 1.
- Digitação no primeiro segundo, antes de `/api/auth/me` e `/api/condicoes` responderem → o que a pessoa escreveu não pode ser sobrescrito pela restauração. Teste: Task 3, Step 6.
- Fechar/recarregar aba menos de 500 ms após digitar → última edição precisa ser gravada no `pagehide`. Teste: Task 2, Step 1.
- Captura com apenas isca, `solto` ou foto → deve voltar preenchida depois da restauração. Teste: Task 2, Step 4.
- Volta do login com `lat`/`lon` ainda no `voltar` → condições devem ser reaplicadas sobre o rascunho e o banner aparecer. Teste: Task 3, Step 10.

---

### Task 1: Formulário anônimo ativo e banner compacto

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro` (markup de `#acesso-registrar`, remoção de `bloquearFormulario`, CSS do banner)
- Modify: `Projetos/fisga/web/tests/service-worker-privacidade.spec.ts:374-393`
- Create: `Projetos/fisga/web/tests/registrar-anonimo.spec.ts`

**Interfaces:**
- Consumes: `#acesso-registrar`, `#pescaria-form`, `limparCachePrivado({ preservarRascunho: true })` de `web/src/lib/auth.ts`.
- Produces: `mockAnonimo(page)` e `mockCondicoes(page)` no novo spec; nenhuma função nova no produto nesta task além da remoção de `bloquearFormulario()`.

- [ ] **Step 1: Atualizar o teste antigo de privacidade**

Em `service-worker-privacidade.spec.ts`, trocar o título de `registrar anônimo preserva o rascunho e bloqueia o formulário` para `registrar anônimo preserva o rascunho e mantém o formulário ativo`. Manter o `page.route` de `/api/auth/me` anônimo e o `expectPublicoPreservadoEPrivadoRemovido(page, { preservarRascunho: true })`, e acrescentar:

```ts
await expect(page.locator('#acesso-registrar')).toBeVisible();
await expect(page.locator('#pescaria-form')).not.toHaveAttribute('inert', /.*/);
```

- [ ] **Step 2: Rodar o teste e verificar que falha**

Run: `npm run test:e2e -- tests/service-worker-privacidade.spec.ts -g "formulário ativo"`
Expected: FAIL — `#pescaria-form` ainda tem `inert` e `#acesso-registrar` ainda é o cartão antigo.

- [ ] **Step 3: Criar o spec novo com o primeiro teste**

Em `tests/registrar-anonimo.spec.ts`, importar `{ test, expect, type Page } from '@playwright/test'`, definir `PORTA`, `ORIGEM = http://localhost:${PORTA}`, `API = 'http://localhost:5184'`, `CORS`, `mockAnonimo(page: Page)` (200 anônimo) e `mockCondicoes(page: Page)` (200 com `{ local: { nome, cidade, estado, ambiente }, temMare: true, mare: { estado: 'subindo', proximoEvento: null }, solunar: { fase: 'Crescente', faseNome: 'crescente' }, tempo: { ventoKmh: 12, chuvaProbabilidade: 20 }, atualizadoEm: '2026-11-19T08:00:00Z' }`). Escrever o teste:

```ts
test('deslogado abre com formulário ativo, banner compacto e nenhuma primária de conta', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await expect(page.locator('#acesso-registrar')).toContainText('Você pode preencher agora. Entrar só quando quiser salvar.');
  await expect(page.locator('#pescaria-form')).not.toHaveAttribute('inert', /.*/);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /2 As condições/ }).click();
  await expect(page.locator('[data-etapa="2"]')).toBeVisible();
  await expect(page).toHaveURL(/\/registrar$/);
});
```

- [ ] **Step 4: Rodar o teste novo e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "formulário ativo"`
Expected: FAIL — `inert` impede o `fill` e a navegação de etapa.

- [ ] **Step 5: Implementar o formulário ativo**

Em `registrar.astro`: apagar `bloquearFormulario()` e a chamada em `iniciar()`; substituir o conteúdo de `#acesso-registrar` pelo banner de uma linha com o texto e o link `Entrar` do Global Constraints (mesmo `href` de `retornoRegistro`); `liberarFormulario()` vira `mostrarOuOcultarBannerAutenticacao()` que só alterna `.oculto` no banner e nunca toca em `inert`/`form-bloqueado`. Ajustar o CSS de `.aviso-acesso` para banner compacto (ícone de cadeado aberto, texto + link, sem card grande) mantendo os tokens.

- [ ] **Step 6: Rodar os testes da task**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts tests/service-worker-privacidade.spec.ts`
Expected: PASS, inclusive o teste renomeado de logout.

- [ ] **Step 7: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-anonimo.spec.ts Projetos/fisga/web/tests/service-worker-privacidade.spec.ts
git commit -m "feat(fisga): manter /registrar ativo para visitantes anônimos"
```

### Task 2: Rascunho local anônimo com flush e capturas completas

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro` (`coletar`, `salvarRascunho`, `agendarRascunho`, listeners de input/change, markup do indicador)
- Modify: `Projetos/fisga/web/tests/registrar-anonimo.spec.ts`

**Interfaces:**
- Consumes: `CHAVE_RASCUNHO` de `web/src/lib/auth.ts`; `coletar()` e `preencher()` existentes.
- Produces: `atualizarIndicadorRascunho(): void`, `flushRascunho(): void`, `#rascunho-indicador` (`role="status"`), `capturasTocadas: boolean`. Task 3 usa `flushRascunho()` antes de navegar para `/entrar`.

- [ ] **Step 1: Teste de flush antes dos 500 ms**

```ts
test('rascunho anônimo sobrevive a recarregar antes do debounce', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.reload();
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#rascunho-indicador')).toContainText('Rascunho salvo neste aparelho');
});
```

- [ ] **Step 2: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "antes do debounce"`
Expected: FAIL — `#local` volta vazio e `#rascunho-indicador` não existe.

- [ ] **Step 3: Implementar autosave anônimo, flush e indicador**

Em `registrar.astro`: remover os `if (autenticado)` dos listeners de `input`/`change`; extrair `flushRascunho()` que limpa `timerRascunho` e chama `salvarRascunho()`; registrar `pagehide` e `beforeunload` para `flushRascunho`; `salvarRascunho()` chama `atualizarIndicadorRascunho()`, que escreve `✓ Rascunho salvo neste aparelho · HH:MM` em `#rascunho-indicador` (inserido logo após `#erro-form`, `role="status"`). `restaurarRascunho()` continua retornando `.entrada`.

- [ ] **Step 4: Teste de captura só com isca/solto/foto**

```ts
test('captura com apenas isca, solto e foto volta do rascunho', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: {
      local: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar', data: '2026-11-19',
      periodo: 'Tarde', mare: 'Enchente', lua: 'Crescente', clima: 'Sol', clarezaDaAgua: 'NaoInformada',
      privacidadeDoLocal: 'Exato', observacoes: null, pescador: null,
      capturas: [{ especie: '', pesoKg: 0, comprimentoCm: 0, isca: 'Camarão', solto: true,
        fotoUrl: 'http://localhost:5184/foto.jpg', fotos: ['http://localhost:5184/foto.jpg'], observacao: null }]
    },
    atualizadoEm: '2026-11-19T08:00:00Z'
  })));
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await expect(page.locator('.captura-isca')).toHaveValue('Camarão');
  await expect(page.locator('.captura-solto-check')).toBeChecked();
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
});
```

- [ ] **Step 5: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "apenas isca"`
Expected: FAIL — `coletar()` descarta a linha e a restauração cria uma captura vazia.

- [ ] **Step 6: Implementar o filtro de capturas e a marcação de capturas tocadas**

Em `coletar()`, trocar o `.filter(...)` por um predicado que mantém a linha quando qualquer um destes é truthy: `especie`, `pesoKg`, `comprimentoCm`, `isca`, `solto`, `fotoUrl`, `fotos.length`, `observacao`. Em `criarLinhaCaptura()`, preencher também `fotos`/`fotoUrl` e marcar `capturasTocadas = true` em qualquer `input`/`change` das linhas, clique em adicionar/remover e troca de foto; `preencher()` usa `capturasTocadas` na Task 3.

- [ ] **Step 7: Rodar os testes da task**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts`
Expected: PASS nos dois testes novos e no teste da Task 1.

- [ ] **Step 8: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-anonimo.spec.ts
git commit -m "feat(fisga): salvar rascunho anônimo com flush e capturas completas"
```

### Task 3: Precedência das condições da URL, campos tocados e modo edição

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro` (`preencher`, `aplicarCondicoesDaUrl`, `iniciar`, `mostrarErro`)
- Modify: `Projetos/fisga/web/tests/registrar-anonimo.spec.ts`

**Interfaces:**
- Consumes: `flushRascunho()`, `capturasTocadas` (Task 2), `mockCondicoes(page)` (Task 1).
- Produces: `buscarCondicoesDaUrl(): Promise<{ entrada, texto } | null>`, `mesclarCondicoesComRascunho(condicoes, rascunho): object`, `mostrarCondicoesAplicadas(texto): void`, `camposTocados: Set<string>`, `preencher(entrada, opcoes?: { preservarTocados?: boolean })`. Task 4 consome `flushRascunho()` e `irParaEntrar()`.

- [ ] **Step 1: Teste de edição intacta**

```ts
test('?editar= não é sobrescrito por rascunho nem condições da URL', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Rascunho velho', capturas: [] }, atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await page.route(`${API}/api/pescarias/editar-1`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ id: 'editar-1', shareToken: 'abc', entrada: {
      local: 'Salvo no diário', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar',
      data: '2026-10-10', periodo: 'Manha', mare: 'Vazante', lua: 'Cheia', clima: 'Nublado',
      clarezaDaAgua: 'NaoInformada', privacidadeDoLocal: 'Exato', observacoes: null, capturas: []
    } })
  }));
  await mockCondicoes(page);
  await page.route(`${API}/api/auth/me`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ autenticado: true, usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' }, consentimentoPendente: false, googleConectado: false, temSenha: true })
  }));
  await page.goto(`${ORIGEM}/registrar?editar=editar-1&lat=-23.9&lon=-46.3`);
  await expect(page.locator('#local')).toHaveValue('Salvo no diário');
  await expect(page.locator('#condicoes-aplicadas')).toBeHidden();
});
```

- [ ] **Step 2: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "editar="`
Expected: FAIL — com a nova ordem, condições/rascunho sobrescrevem a pescaria em edição.

- [ ] **Step 3: Separar busca de condições de aplicação**

Em `registrar.astro`, substituir `aplicarCondicoesDaUrl()` por `buscarCondicoesDaUrl()`. Assinatura: `Promise<{ entrada, texto } | null>`; continua exigindo `lat`/`lon`, faz o mesmo `fetch` e a mesma derivação de `data`, `periodo`, `mare`, `lua`, `clima`, retornando `entrada` **apenas** com `local`, `cidade`, `estado`, `ambiente`, `data`, `periodo`, `mare`, `lua`, `clima` e `texto` com a linha de valores do banner. Não chama `preencher()` e não mexe no banner. Retorna `null` sem `lat`/`lon` ou em erro de rede.

- [ ] **Step 4: Implementar mesclagem e banner**

Adicionar `mesclarCondicoesComRascunho(condicoes, rascunho)`: começa de `condicoes.entrada` e completa as chaves ausentes com `rascunho`; `capturas`, `pescador`, `observacoes`, `clarezaDaAgua` e `privacidadeDoLocal` vêm do rascunho quando presentes. Adicionar `mostrarCondicoesAplicadas(texto)` que escreve em `#condicoes-texto` o texto fixo do Global Constraints + `texto` e remove `.oculto` de `#condicoes-aplicadas`. Manter `#ocultar-condicoes` funcionando.

- [ ] **Step 5: Unificar a ordem de carregamento em `iniciar()`**

Depois do `/api/auth/me`: se `?editar` e autenticado → `iniciarEdicao()` e `return` (sem ler rascunho nem condições). Caso contrário: chamar `mostrarOuOcultarBannerAutenticacao()`; ler `restaurarRascunho()`; `const condicoes = await buscarCondicoesDaUrl()`; se houver condições, `preencher(mesclarCondicoesComRascunho(condicoes, rascunho), { preservarTocados: true })` e `mostrarCondicoesAplicadas(condicoes.texto)`; senão, se houver rascunho, `preencher(rascunho, { preservarTocados: true })`; senão `iniciarFormularioVazio()`. Chamar `aplicarEspecieDaWiki()` ao final. Erro ao ler rascunho mostra `Não conseguimos recuperar o rascunho. Você pode continuar preenchendo.` em `#erro-form` via `mostrarErro()` e segue com formulário vazio.

- [ ] **Step 6: Teste de campos tocados durante a inicialização**

Definir `async function mockCondicoesLentas(page: Page, atrasoMs: number)` ao lado de `mockCondicoes`, com o mesmo payload e `await new Promise((resolve) => setTimeout(resolve, atrasoMs))` antes do `route.fulfill`.

```ts
test('digitação antes da resposta das condições não é sobrescrita', async ({ page }) => {
  await mockAnonimo(page);
  await mockCondicoesLentas(page, 600);
  await page.goto(`${ORIGEM}/registrar?lat=-23.9&lon=-46.3&nome=Ilhabela`);
  await page.locator('#observacoes').fill('Anotação digitada primeiro');
  await expect(page.locator('#observacoes')).toHaveValue('Anotação digitada primeiro');
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#condicoes-aplicadas')).toBeVisible();
});
```

- [ ] **Step 7: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "digitada primeiro"`
Expected: FAIL — `preencher()` sobrescreve `#observacoes` quando as condições chegam.

- [ ] **Step 8: Implementar proteção de campos tocados**

Adicionar `const camposTocados = new Set<string>()` e, no listener global de `input`/`change`, `camposTocados.add((evento.target as HTMLElement).id)`. Em `preencher(entrada, opcoes = {})`, com `preservarTocados !== false`, pular a atribuição de um campo cujo `id` esteja em `camposTocados`; se `capturasTocadas` for `true`, não limpar `listaCapturas` — acrescentar as capturas do rascunho que ainda não existirem (comparação por `JSON.stringify` dos campos relevantes) e criar uma linha vazia apenas se a lista estiver vazia. Chamar `atualizarContador()` e `atualizarDefeso()` ao final.

- [ ] **Step 9: Teste de URL sobre rascunho antigo**

```ts
test('condições do link vencem rascunho antigo e preservam o resto', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Local antigo', cidade: 'Aracaju', estado: 'SE', ambiente: 'Represa',
      data: '2026-01-01', periodo: 'Manha', mare: 'Vazante', lua: 'Minguante', clima: 'Chuva',
      clarezaDaAgua: 'Turva', privacidadeDoLocal: 'Aproximada', observacoes: 'Anotação antiga',
      pescador: 'Marina', capturas: [{ especie: 'Robalo', pesoKg: 2, capturasExtra: 'não usa' }] },
    atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await mockAnonimo(page);
  await mockCondicoes(page);
  await page.goto(`${ORIGEM}/registrar?lat=-23.9&lon=-46.3&nome=Ilhabela&cidade=Ilhabela&estado=SP&ambiente=Mar`);
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#mare')).toHaveValue('Enchente');
  await expect(page.locator('#lua')).toHaveValue('Crescente');
  await expect(page.locator('#clima')).toHaveValue('Sol');
  await expect(page.locator('#observacoes')).toHaveValue('Anotação antiga');
  await expect(page.locator('.captura-especie').first()).toHaveValue('Robalo');
  await expect(page.locator('#condicoes-texto')).toContainText('Os valores do link vieram na frente do seu rascunho');
});
```

- [ ] **Step 10: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "vencem rascunho"`
Expected: FAIL — o rascunho antigo ainda ganha porque `restaurarRascunho()` retornava antes de aplicar condições.

- [ ] **Step 11: Teste do retorno do login com condições no `voltar`**

```ts
test('depois do login, voltar com lat/lon reaplica condições e mostra o banner', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Antigo', data: '2026-01-01', capturas: [] }, atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await page.route(`${API}/api/auth/me`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ autenticado: true, usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' }, consentimentoPendente: false, googleConectado: false, temSenha: true })
  }));
  await mockCondicoes(page);
  await page.goto(`${ORIGEM}/registrar?lat=-23.9&lon=-46.3&nome=Ilhabela`);
  await expect(page.locator('#acesso-registrar')).toBeHidden();
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#condicoes-aplicadas')).toBeVisible();
  await expect(page.locator('#btn-salvar')).toBeVisible();
});
```

- [ ] **Step 12: Rodar todos os testes da task**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts`
Expected: PASS nos cinco cenários (edição, campos tocados, URL sobre rascunho, retorno do login e anteriores).

- [ ] **Step 13: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-anonimo.spec.ts
git commit -m "feat(fisga): condições do link prevalecem sobre rascunho sem perder digitação"
```

### Task 4: Foto anônima e confirmação antes de salvar

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro` (template de `criarLinhaCaptura`, handler do arquivo, markup do painel, handler de `submit`, CSS)
- Modify: `Projetos/fisga/web/tests/registrar-anonimo.spec.ts`

**Interfaces:**
- Consumes: `flushRascunho()` (Task 2), `irParaEntrar()`, `camposTocados` (Task 3), `#btn-salvar`.
- Produces: `.foto-nota` por linha de captura; `#confirmar-salvar`, `#confirmar-entrar`, `#confirmar-continuar`.

- [ ] **Step 1: Teste da nota de foto anônima**

```ts
test('foto anônima mostra nota inline e não redireciona', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('.captura-foto-adicionar').click();
  await expect(page.locator('.foto-nota')).toBeVisible();
  await expect(page.locator('.foto-nota')).toContainText('A foto entra depois que você entra.');
  await expect(page).toHaveURL(/\/registrar$/);
  await expect(page.locator('input[type="file"]').first()).toHaveJSProperty('files.length', 0);
});
```

- [ ] **Step 2: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "foto anônima"`
Expected: FAIL — o clique navega para `/entrar` e não existe `.foto-nota`.

- [ ] **Step 3: Implementar a nota inline**

Em `criarLinhaCaptura()`, acrescentar dentro de `.captura-fotos` um `<p class="foto-nota" role="status" hidden>` com o texto do Global Constraints. No `label.captura-foto-adicionar`, adicionar listener de `click` que chama `evento.preventDefault()` quando `!autenticado` e mostra a nota; no `change` do `input[type=file]`, se `!autenticado`, limpar `inputArquivo.value`, mostrar a nota e `return` antes de qualquer upload. Com sessão, manter o fluxo atual de upload.

- [ ] **Step 4: Teste do painel de confirmação no submit anônimo**

```ts
test('submit anônimo abre confirmação, continua aqui mantém os dados e entrar preserva o volta', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  await page.locator('#btn-salvar').click();
  await expect(page.locator('#confirmar-salvar')).toBeVisible();
  await expect(page.locator('#btn-salvar')).toBeHidden();
  await expect(page).toHaveURL(/\/registrar/);
  await page.locator('#confirmar-continuar').click();
  await expect(page.locator('#confirmar-salvar')).toBeHidden();
  await expect(page.locator('#btn-salvar')).toBeVisible();
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await page.locator('#btn-salvar').click();
  await Promise.all([
    page.waitForURL((url) => url.pathname === '/entrar' && url.searchParams.get('voltar')?.startsWith('/registrar')),
    page.locator('#confirmar-entrar').click()
  ]);
});
```

- [ ] **Step 5: Rodar e verificar que falha**

Run: `npm run test:e2e -- tests/registrar-anonimo.spec.ts -g "confirmação"`
Expected: FAIL — submit anônimo navega direto para `/entrar`.

- [ ] **Step 6: Implementar o painel de confirmação**

Em `registrar.astro`, inserir `#confirmar-salvar` depois de `.etapa-acoes` com o texto exato do Global Constraints, dois botões `#confirmar-entrar` e `#confirmar-continuar` e `role="status"`. No `submit`, trocar `if (!autenticado) { irParaEntrar(); return; }` por `if (!autenticado) { mostrarConfirmacao(); return; }`. `mostrarConfirmacao()` esconde `#btn-salvar`, remove `.oculto` do painel e foca `#confirmar-entrar`. `#confirmar-continuar` fecha o painel, mostra `#btn-salvar` e devolve o foco a ele. `#confirmar-entrar` chama `flushRascunho()` e `irParaEntrar()`. CSS do painel: superfície, 1px de borda, raio 12px, sem scrim, primária petrol e secundária contorno, alvos ≥ 44 px.

- [ ] **Step 7: Teste de regressão do submit autenticado**

```ts
test('com sessão, salvar segue para a pescaria sem painel de confirmação', async ({ page }) => {
  await page.route(`${API}/api/auth/me`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ autenticado: true, usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' }, consentimentoPendente: false, googleConectado: false, temSenha: true })
  }));
  await page.route(`${API}/api/pescarias`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ id: 'nova', shareToken: 'xyz' })
  }));
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  await page.locator('#btn-salvar').click();
  await expect(page).toHaveURL(/\/p\/xyz\?novo=1/);
  await expect(page.locator('#confirmar-salvar')).toBeHidden();
});
```

- [ ] **Step 8: Rodar a suíte completa**

Run: `npm run check && npm run build && npm run test:e2e`
Expected: `astro check` 0 erros, build ok, suíte E2E verde (111 testes do baseline + 10 cenários novos; o teste antigo de `inert` foi renomeado, não duplicado).

- [ ] **Step 9: Commit**

```bash
git add Projetos/fisga/web/src/pages/registrar.astro Projetos/fisga/web/tests/registrar-anonimo.spec.ts
git commit -m "feat(fisga): foto e salvamento anônimos sem sair do registro"
```

### Task 5: Documentação e fechamento

**Files:**
- Modify: `Projetos/fisga/docs/INDEX.md`
- Modify: `Projetos/fisga/docs/ux-audit.md:36`
- Modify: `Projetos/fisga/docs/superpowers/specs/2026-11-19-registrar-anonimo-design.md` (status aprovado)

**Interfaces:**
- Consumes: comportamento entregue nas Tasks 1–4.
- Produces: documento de design com `Status: aprovado`, índice atualizado e nenhuma referência a `inert` em `/registrar`.

- [ ] **Step 1: Atualizar documentação**

Em `docs/INDEX.md`, adicionar em `## Planejamento` a entrada `- [Registrar anônimo](superpowers/specs/2026-11-19-registrar-anonimo-design.md)`. Em `docs/ux-audit.md:36`, trocar `| \`/registrar\` | Registro da pescaria | Conta (formulário visível) |` por `| \`/registrar\` | Registro da pescaria | Público (formulário ativo; conta só no salvamento) |`. No design doc, trocar `**Status:** aguardando aprovação` por `**Status:** aprovado`.

- [ ] **Step 2: Rodar lint e verificação final**

Run: `npx --yes markdownlint-cli2@0.24.1 && npm run check && npm run build && npm run test:e2e --prefix Projetos/fisga/web`
Expected: 0 issues de Markdown, 0 erros de `astro check`, build ok, suíte E2E verde.

- [ ] **Step 3: Revisar o diff antes do PR**

Run: `git diff --stat origin/main...HEAD && git status --short`
Expected: só os arquivos listados nas Tasks 1–5; nenhum artefato `.superpowers/`, nenhum `test-results/`, nenhum segredo.

- [ ] **Step 4: Commit**

```bash
git add Projetos/fisga/docs
git commit -m "docs(fisga): registrar anônimo como fluxo de captura primeiro"
```
