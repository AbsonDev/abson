# Upload de fotos com progresso, retry e HEIC — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dar estado individual a cada foto enviada em `/registrar`, com progresso real, retry que não duplica concluídas e tratamento claro de HEIC.

**Architecture:** toda a mudança é client-side em `web/src/pages/registrar.astro` + `web/src/styles/global.css`. O `POST /api/midia` continua o mesmo (`{ url }`), com `withCredentials`. Um `WeakMap<linha, Entrada[]>` guarda o estado das fotos em memória; a URL final segue indo para `dataset.fotos` e para o rascunho. Sem API, banco, contrato ou configuração novos.

**Tech Stack:** Astro SSR, TypeScript no script inline da página, CSS global, Playwright mockado.

**Spec:** `../specs/2026-11-19-upload-fotos-progresso-retry-design.md`
**Issue:** #52

## Global Constraints

- Ler `Projetos/fisga/AGENTS.md`, `docs/INDEX.md` e a spec antes de editar.
- UI/copy em pt-BR, sem emoji, sem "lorem ipsum".
- Tokens **reais** do web (`global.css:1-26`): `--tinta` `#172a2d`, `--tinta-suave` `#657277`, `--linha` `#e3d8c8`, `--terracota` `#174e57` (é o petrol, apesar do nome), `--acafrao` `#d9773f` (é o clay), `--superficie` `#fffdf8`, `--raio-pequeno` `12px`. Não criar cor nova. Os nomes `--petrol`/`--clay` existem só no artefato do OpenDesign; no produto usar os tokens acima.
- Nenhuma dependência nova. `heic2any`/WASM está fora (spec, abordagem A recusada).
- Alvo mínimo de 44 px, foco visível de 3 px, erro sempre com palavra + ícone/estado, nunca só cor.
- Barra de progresso usa **só** `transform`/`opacity`; nunca animar `width`. `prefers-reduced-motion` remove a transição e mantém o texto.
- `uploadsPendentes` passa a contar **entradas em envio**, não lotes. O `#btn-salvar` e a checagem em `:1391` continuam usando essa variável; não mudar a mensagem "Espere o envio das fotos terminar."
- 401 continua chamando `encerrarSessaoExpirada(urlDeRetorno())` e marcando `descartarRascunho = true`.
- Comandos (raiz `Projetos/fisga`): `export PATH="$HOME/.dotnet:$PATH"`, `npm ci` em `web/`, `npm run check`, `npm run build`, `npm run test:e2e`, `npx --yes markdownlint-cli2@0.24.1`.
- O Playwright sobe `npm run start`, que serve `dist/` — **rodar `npm run build` antes de cada `npm run test:e2e`**.
- Um commit por task, mensagem convencional, sem segredos.

## Review Focus

| Risco | Onde é fixado |
| --- | --- |
| Retry reenvia o lote e duplica fotos já salvas | Task 2: `enviarEntrada` recebe uma única entrada; `definirFotos` usa `Set` |
| HEIC inválido aborta as outras fotos | Task 2: `catch` por entrada, não por lote |
| HEIC que decodifica mas falha na rede perde o retry | Task 2 Step 5/5b: só erro de decode vira `heic-recusado`; erro de transporte vira `falhou` |
| Barra inventa percentual | Task 2: `onprogress` só quando `lengthComputable`; barra é `aria-hidden` |
| `#btn-salvar` volta a habilitar antes do fim | Task 2: `uploadsPendentes` por entrada + `atualizarBotoes()` |
| Slot de foto travado por foto falha | Task 2: só `enviado` e `enviando` contam para `MAX_FOTOS` |
| `renderizarFotos()` apaga os chips de upload | Task 2: `.upload-lista` separada de `.captura-foto-thumb` |
| Estilo novo fogue do design system | Task 3: só tokens existentes; 44 px; `focus-visible` de 3 px |
| Texto de erro com contraste baixo | Task 3: `--acafrao` fica na borda; o texto usa `--tinta` com peso 700 |

---

### Task 1: E2E RED — descrever os quatro comportamentos

**Files:**
- Create: `Projetos/fisga/web/tests/registrar-upload.spec.ts`

**Interfaces:**
- Consumes: `/registrar` autenticado (mock de `/api/auth/me`), `POST http://localhost:5184/api/midia`, `public/icon-512.png` como fixture decodificável.
- Produces: 5 testes que hoje falham porque não existe `.upload-lista`, `.upload-item--falhou` nem `image/heic` no `accept`.

- [ ] **Step 1: criar a suíte**

```ts
import { readFileSync } from 'node:fs';
import { test, expect, type Page } from '@playwright/test';

const API = 'http://localhost:5184';
const ORIGEM = `http://localhost:${process.env.PORT ?? 4323}`;
const CORS = {
  'access-control-allow-origin': ORIGEM,
  'access-control-allow-credentials': 'true'
};
// PNG real do próprio projeto: o createImageBitmap decodifica por assinatura,
// mesmo quando o nome diz .heic — é assim que simulamos o decode nativo.
const PNG = readFileSync(new URL('../public/icon-512.png', import.meta.url));

async function abrirRegistrar(page: Page) {
  await page.route('**/api/auth/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        autenticado: true,
        usuario: { id: '1', email: 'pescador@fisga.app', nomeExibicao: 'Marina Sá' },
        consentimentoPendente: false
      })
    })
  );
  await page.goto('/registrar');
  await expect
    .poll(() => page.locator('#pescaria-form').evaluate((el) => (el as HTMLElement).inert))
    .toBe(false);
  await page.locator('[data-etapa="3"]').evaluate((el) => el.removeAttribute('hidden'));
}

test('aceita HEIC no seletor e envia quando o navegador decodifica', async ({ page }) => {
  await abrirRegistrar(page);
  const seletor = page.locator('input[type="file"]').first();
  for (const tipo of ['image/jpeg', 'image/png', 'image/webp', 'image/heic', 'image/heif']) {
    await expect(seletor).toHaveAttribute('accept', new RegExp(tipo));
  }
  await page.route(`${API}/api/midia`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({ url: '/midia/heic.jpg' })
    })
  );

  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'foto.heic',
    mimeType: 'image/heic',
    buffer: PNG
  });

  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
  await expect(page.locator('.upload-item--heic')).toHaveCount(0);
});

test('HEIC que o navegador não decodifica mostra aviso e não bloqueia o formulário', async ({ page }) => {
  await abrirRegistrar(page);
  await page.route(`${API}/api/midia`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({ url: '/midia/valida.jpg' })
    })
  );

  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'ruim.heic',
    mimeType: 'image/heic',
    buffer: Buffer.from('nao-e-uma-imagem')
  });
  await expect(page.locator('.upload-item--heic')).toContainText(
    'Este iPhone salvou a foto em HEIC. Abra a foto e exporte como JPEG, ou ative JPEG em Ajustes → Câmera → Formatos.'
  );
  await expect(page.locator('.upload-retry')).toHaveCount(0);
  await expect(page.locator('#btn-salvar')).toBeEnabled();

  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'valida.jpg',
    mimeType: 'image/jpeg',
    buffer: PNG
  });
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
});

test('mostra progresso por foto e só habilita Salvar depois do envio', async ({ page }) => {
  await abrirRegistrar(page);
  let liberar!: () => void;
  const barreira = new Promise<void>((resolve) => { liberar = resolve; });
  await page.route(`${API}/api/midia`, async (route) => {
    await barreira;
    await route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({ url: '/midia/um.jpg' })
    });
  });

  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'um.jpg',
    mimeType: 'image/jpeg',
    buffer: PNG
  });

  const enviando = page.locator('.upload-item--enviando');
  await expect(enviando).toBeVisible();
  await expect(enviando).toContainText('Enviando…');
  await expect(enviando.locator('.upload-barra')).toHaveAttribute('aria-hidden', 'true');
  await expect(page.locator('.upload-status')).toContainText('Enviando 1 de 1 fotos…');
  await expect(page.locator('#btn-salvar')).toBeDisabled();

  liberar();
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
  await expect(page.locator('.upload-item')).toHaveCount(0);
  await expect(page.locator('.upload-status')).toHaveText('Fotos enviadas.');
  await expect(page.locator('#btn-salvar')).toBeEnabled();
});

test('retry repete só a foto que falhou sem duplicar as que subiram', async ({ page }) => {
  await abrirRegistrar(page);
  let chamadas = 0;
  await page.route(`${API}/api/midia`, (route) => {
    chamadas += 1;
    const falhou = chamadas === 1;
    return route.fulfill({
      status: falhou ? 500 : 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify(falhou
        ? { codigo: 'erro', detalhe: 'Falha de rede simulada.' }
        : { url: `/midia/foto-${chamadas}.jpg` })
    });
  });

  await page.locator('input[type="file"]').first().setInputFiles([
    { name: 'primeira.jpg', mimeType: 'image/jpeg', buffer: PNG },
    { name: 'segunda.jpg', mimeType: 'image/jpeg', buffer: PNG }
  ]);

  await expect(page.locator('.upload-item--falhou')).toHaveCount(1);
  await expect(page.locator('.upload-item--falhou')).toContainText('Não enviada');
  await expect(page.locator('.upload-item--falhou')).toContainText(
    'Não conseguimos enviar esta foto. Só ela precisa repetir — as outras já estão salvas.'
  );
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
  await expect(page.locator('#btn-salvar')).toBeEnabled();

  await page.getByRole('button', { name: 'Tentar de novo' }).click();
  await expect(page.locator('.upload-item')).toHaveCount(0);
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(2);
  await expect(page.locator('.linha-captura').first()).toHaveAttribute(
    'data-fotos',
    /\/midia\/foto-2\.jpg/
  );
  expect(chamadas).toBe(3); // 1 falha + 1 sucesso + 1 retry — nunca o lote inteiro
});

test('foto falha não ocupa slot: dá para adicionar outra sem apagar o retry', async ({ page }) => {
  await abrirRegistrar(page);
  let chamadas = 0;
  await page.route(`${API}/api/midia`, (route) => {
    chamadas += 1;
    const falhou = chamadas === 1;
    return route.fulfill({
      status: falhou ? 503 : 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify(falhou
        ? { detalhe: 'Indisponível.' }
        : { url: '/midia/outra.jpg' })
    });
  });
  const input = page.locator('input[type="file"]').first();
  // PNG decodificável: a falha vem do 503 da API, não do createImageBitmap.
  await input.setInputFiles({ name: 'falha.jpg', mimeType: 'image/jpeg', buffer: PNG });
  await expect(page.locator('.upload-item--falhou')).toBeVisible();
  await input.setInputFiles({ name: 'outra.jpg', mimeType: 'image/jpeg', buffer: PNG });
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
  await expect(page.locator('.upload-item--falhou')).toHaveCount(1);
  expect(chamadas).toBe(2);
});
```

- [ ] **Step 2: rodar e confirmar RED**

Run: `cd Projetos/fisga/web && npm ci && npm run build && npx playwright test tests/registrar-upload.spec.ts --reporter=list`
Expected: FAIL — `accept` sem HEIC, `.upload-lista`/`.upload-item` inexistentes. Não é falha de servidor; o `npm run build` é pré-requisito do `npm run start`.

- [ ] **Step 3: commit da suíte**

```bash
git add Projetos/fisga/web/tests/registrar-upload.spec.ts
git commit -m "test(fisga): cobre progresso, retry e HEIC no registrar"
```

---

### Task 2: GREEN — máquina de estado por foto no `registrar.astro`

**Files:**
- Modify: `Projetos/fisga/web/src/pages/registrar.astro`
- Modify (Step 5b): `Projetos/fisga/web/tests/registrar-upload.spec.ts`

**Interfaces:**
- Consumes: `fotosDaLinha`, `capaDaLinha`, `definirFotos`, `renderizarFotos`, `aplicarExif`, `comprimirImagem`, `sugerirEspecie`, `agendarRascunho`, `mostrarErro`, `encerrarSessaoExpirada`, `uploadsPendentes`, `botaoSalvar`, `MAX_FOTOS`.
- Produces: `WeakMap uploadsPorLinha`, `entradasDe`, `renderizarUploads`, `enviarEntrada`, `enviarMidia`, `SessaoExpirada`, `ehHeic`, `atualizarBotoes`.

- [ ] **Step 1: aceitar HEIC no input**

Em `criarLinhaCaptura()` (`:597`), trocar:

```html
<input type="file" accept="image/jpeg,image/png,image/webp" multiple />
```

por:

```html
<input type="file" accept="image/jpeg,image/png,image/webp,image/heic,image/heif,.heic,.heif" multiple />
```

- [ ] **Step 2: adicionar container e estrutura de estado**

No template da linha (`:595-601`), dentro de `.captura-fotos`, depois do `<label class="captura-foto-adicionar">` e antes da `.foto-nota`, inserir:

```html
<div class="upload-lista" aria-live="polite"></div>
<p class="upload-status" role="status" aria-live="polite"></p>
```

Declarações novas ao lado de `const MAX_FOTOS = 4;` (`:511`):

```js
const uploadsPorLinha = new WeakMap();
let sequenciaUploads = 0;
let exifDaSelecaoTentado = false;

class SessaoExpirada extends Error {}

function entradasDe(linha) {
  if (!uploadsPorLinha.has(linha)) uploadsPorLinha.set(linha, []);
  return uploadsPorLinha.get(linha);
}

function ehHeic(arquivo) {
  return /heic|heif/i.test(arquivo.type) || /\.(heic|heif)$/i.test(arquivo.name);
}

function atualizarBotoes() {
  botaoSalvar.disabled = uploadsPendentes > 0;
}

function vagasNaLinha(linha) {
  const ocupadas = fotosDaLinha(linha).length
    + entradasDe(linha).filter((entrada) => entrada.estado === 'enviando').length;
  return Math.max(0, MAX_FOTOS - ocupadas);
}
```

- [ ] **Step 3: renderizar chips de upload**

Adicionar ao lado de `renderizarFotos()` (`:535`), sem mexer na função existente:

```js
function renderizarUploads(linha) {
  const container = linha.querySelector('.captura-fotos');
  const lista = container.querySelector('.upload-lista');
  const status = container.querySelector('.upload-status');
  const adicionar = container.querySelector('.captura-foto-adicionar');
  const input = container.querySelector('input[type="file"]');
  if (!lista || !status || !adicionar || !input) return;

  const entradas = entradasDe(linha);
  const enviando = entradas.filter((entrada) => entrada.estado === 'enviando').length;
  lista.replaceChildren();

  for (const entrada of entradas) {
    const item = document.createElement('div');
    item.className = `upload-item upload-item--${entrada.estado}`;
    // O estado é `heic-recusado`; o teste e os estilos usam `.upload-item--heic`.
    if (entrada.estado === 'heic-recusado') item.classList.add('upload-item--heic');
    item.dataset.uploadId = entrada.id;

    const nome = document.createElement('span');
    nome.className = 'upload-nome';
    nome.textContent = entrada.nome;
    item.appendChild(nome);

    if (entrada.estado === 'enviando') {
      const estado = document.createElement('span');
      estado.className = 'upload-estado';
      estado.textContent = 'Enviando…';
      const barra = document.createElement('div');
      barra.className = 'upload-barra';
      barra.setAttribute('aria-hidden', 'true');
      const preenchimento = document.createElement('span');
      preenchimento.style.transform = `scaleX(${Math.min(1, Math.max(0, entrada.progresso))})`;
      barra.appendChild(preenchimento);
      item.append(estado, barra);
    } else if (entrada.estado === 'falhou') {
      const estado = document.createElement('span');
      estado.className = 'upload-estado';
      estado.textContent = 'Não enviada';
      if (entrada.mensagem) estado.title = entrada.mensagem;
      const nota = document.createElement('p');
      nota.className = 'upload-nota';
      nota.textContent = 'Não conseguimos enviar esta foto. Só ela precisa repetir — as outras já estão salvas.';
      const retry = document.createElement('button');
      retry.type = 'button';
      retry.className = 'upload-retry';
      retry.textContent = 'Tentar de novo';
      retry.addEventListener('click', () => {
        if (entrada.estado === 'falhou') enviarEntrada(linha, entrada);
      });
      item.append(estado, nota, retry);
    } else if (entrada.estado === 'heic-recusado') {
      const nota = document.createElement('p');
      nota.className = 'upload-heic';
      nota.textContent = 'Este iPhone salvou a foto em HEIC. Abra a foto e exporte como JPEG, ou ative JPEG em Ajustes → Câmera → Formatos.';
      item.appendChild(nota);
    }
    lista.appendChild(item);
  }

  const falhas = entradas.filter((entrada) => entrada.estado === 'falhou').length;
  status.textContent = enviando > 0
    ? `Enviando ${enviando} de ${entradas.length} fotos…`
    : falhas > 0
      ? (falhas === 1 ? '1 foto não foi enviada. Use Tentar de novo.' : `${falhas} fotos não foram enviadas. Use Tentar de novo.`)
      : 'Fotos enviadas.';

  adicionar.classList.toggle('oculto', vagasNaLinha(linha) <= 0);
  input.disabled = enviando > 0;
  atualizarBotoes();
}
```

Nota: `renderizarFotos()` continua removendo só `.captura-foto-thumb`; por isso `.upload-lista` não é apagada quando uma URL entra em `dataset.fotos`.

- [ ] **Step 4: transporte com progresso real**

Adicionar ao lado de `comprimirImagem()` (`:854`):

```js
function enviarMidia(blob, aoProgressar) {
  return new Promise((resolve, reject) => {
    const requisicao = new XMLHttpRequest();
    requisicao.open('POST', `${API}/api/midia`);
    requisicao.withCredentials = true;

    requisicao.upload.addEventListener('progress', (evento) => {
      if (evento.lengthComputable && evento.total > 0) {
        aoProgressar(evento.loaded / evento.total);
      }
    });
    requisicao.addEventListener('load', () => {
      if (requisicao.status === 401) {
        reject(new SessaoExpirada());
        return;
      }
      let dados = {};
      try { dados = JSON.parse(requisicao.responseText || '{}'); } catch { /* resposta sem corpo */ }
      if (requisicao.status < 200 || requisicao.status >= 300) {
        reject(new Error(dados.detalhe || 'Não foi possível enviar a foto.'));
        return;
      }
      if (!dados.url) {
        reject(new Error('Não foi possível enviar a foto.'));
        return;
      }
      resolve(dados);
    });
    requisicao.addEventListener('error', () =>
      reject(new Error('Não foi possível enviar a foto. Verifique sua conexão.'))
    );
    requisicao.addEventListener('abort', () => reject(new Error('Envio cancelado.')));

    const formulario = new FormData();
    formulario.append('arquivo', blob, 'foto.jpg');
    requisicao.send(formulario);
  });
}
```

- [ ] **Step 5: enviar uma entrada por vez**

```js
async function enviarEntrada(linha, entrada) {
  entrada.estado = 'enviando';
  entrada.progresso = 0;
  uploadsPendentes += 1;
  renderizarUploads(linha);
  // Só erro de decode/compressão de HEIC vira "exporte como JPEG"; erro de
  // transporte/servidor continua sendo falha com "Tentar de novo".
  let fase = 'processar';

  try {
    if (!exifDaSelecaoTentado) {
      exifDaSelecaoTentado = true;
      await aplicarExif(entrada.arquivo);
    }
    const blob = await comprimirImagem(entrada.arquivo);
    fase = 'enviar';
    const dados = await enviarMidia(blob, (progresso) => {
      entrada.progresso = progresso;
      renderizarUploads(linha);
    });

    const fotos = fotosDaLinha(linha);
    if (fotos.length < MAX_FOTOS) {
      fotos.push(dados.url);
      definirFotos(linha, fotos, capaDaLinha(linha) || dados.url);
      agendarRascunho();
    }
    await sugerirEspecie(linha, blob);
    const entradas = entradasDe(linha);
    const indice = entradas.indexOf(entrada);
    if (indice >= 0) entradas.splice(indice, 1);
  } catch (erro) {
    if (erro instanceof SessaoExpirada) {
      descartarRascunho = true;
      await encerrarSessaoExpirada(urlDeRetorno());
      return;
    }
    entrada.estado = fase === 'processar' && ehHeic(entrada.arquivo) ? 'heic-recusado' : 'falhou';
    entrada.mensagem = erro instanceof Error ? erro.message : 'Não foi possível enviar a foto.';
  } finally {
    uploadsPendentes -= 1;
    renderizarUploads(linha);
  }
}
```

- [ ] **Step 5b: regression — HEIC decodificado com erro de servidor dá retry**

Acrescentar em `web/tests/registrar-upload.spec.ts`:

```ts
test('HEIC decodificado com erro de servidor oferece retry', async ({ page }) => {
  await abrirRegistrar(page);
  let chamadas = 0;
  await page.route(`${API}/api/midia`, (route) => {
    chamadas += 1;
    const falhou = chamadas === 1;
    return route.fulfill({
      status: falhou ? 503 : 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify(falhou
        ? { detalhe: 'Indisponível.' }
        : { url: '/midia/heic-retry.jpg' })
    });
  });

  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'foto.heic',
    mimeType: 'image/heic',
    buffer: PNG
  });
  await expect(page.locator('.upload-item--falhou')).toContainText('Não enviada');
  await expect(page.getByRole('button', { name: 'Tentar de novo' })).toBeVisible();
  await expect(page.locator('.upload-item--heic')).toHaveCount(0);

  await page.getByRole('button', { name: 'Tentar de novo' }).click();
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
  await expect(page.locator('.upload-item')).toHaveCount(0);
  expect(chamadas).toBe(2);
});
```

- [ ] **Step 6: trocar o handler de `change`**

Substituir o bloco `:632-699` por:

```js
inputArquivo.addEventListener('change', async () => {
  if (!autenticado) {
    inputArquivo.value = '';
    notaFoto.hidden = false;
    return;
  }

  const arquivos = [...(inputArquivo.files || [])];
  if (arquivos.length === 0) return;

  const entradas = entradasDe(linha);
  // Avisos HEIC já lidos não ocupam slot nem precisam de retry.
  for (let i = entradas.length - 1; i >= 0; i -= 1) {
    if (entradas[i].estado === 'heic-recusado') entradas.splice(i, 1);
  }
  const vagas = vagasNaLinha(linha);
  const paraEnviar = arquivos.slice(0, vagas);
  if (arquivos.length > vagas) {
    mostrarErro(vagas === 0
      ? 'Máximo de 4 fotos por captura.'
      : 'Máximo de 4 fotos por captura. As extras foram ignoradas.');
  }
  inputArquivo.value = '';
  if (paraEnviar.length === 0) {
    renderizarUploads(linha);
    return;
  }

  exifDaSelecaoTentado = false;
  const novas = paraEnviar.map((arquivo) => ({
    id: `up-${++sequenciaUploads}`,
    nome: arquivo.name,
    arquivo,
    estado: 'enviando',
    progresso: 0,
    mensagem: ''
  }));
  entradas.push(...novas);
  renderizarUploads(linha);

  for (const entrada of novas) await enviarEntrada(linha, entrada);
});
```

Chamar também `renderizarUploads(linha)` logo ao lado de `renderizarFotos(linha)` em `:708`, para que linhas restauradas do rascunho nascente comecem com o status e o `#btn-salvar` coerentes.

O `finally` antigo (`uploadsPendentes -= 1; inputArquivo.disabled = false; inputArquivo.value = ''; botaoSalvar.disabled = uploadsPendentes > 0;`) sai: agora cada `enviarEntrada` faz sua própria contabilização e `renderizarUploads` controla o `disabled` do input daquela linha.

- [ ] **Step 7: validar GREEN**

Run: `cd Projetos/fisga/web && npm run check && npm run build && npx playwright test tests/registrar-upload.spec.ts --reporter=list`
Expected: os 5 testes passam. Se `Enviando 1 de 1 fotos…` não aparecer, verificar se o `role="status"` está sendo limpo por `renderizarUploads` antes da asserção — o texto é síncrono no `change`, então deve aparecer antes do `await` do XHR.

- [ ] **Step 8: regressão da suíte e commit**

Run: `npx playwright test --reporter=list`
Expected: suíte mockada passa, incluindo `registrar-anonimo.spec.ts` e `niveis-de-detalhe.spec.ts`.

```bash
git add Projetos/fisga/web/src/pages/registrar.astro
git commit -m "feat(fisga): progresso e retry por foto no registrar"
```

---

### Task 3: estilos dos estados no `global.css`

**Files:**
- Modify: `Projetos/fisga/web/src/styles/global.css`

**Interfaces:**
- Consumes: tokens de `:root`, `.captura-fotos`/`.captura-foto-thumb` (`:3437-3524`).
- Produces: `.upload-lista`, `.upload-item`, `.upload-item--falhou`, `.upload-item--heic`, `.upload-nome`, `.upload-estado`, `.upload-barra`, `.upload-nota`, `.upload-heic`, `.upload-retry`, `.upload-status`.

- [ ] **Step 1: adicionar os estilos depois de `.captura-foto-thumb .capa-selo` (`:3524`)**

```css
.upload-lista {
  display: grid;
  flex: 0 0 100%;
  gap: 8px;
}

.upload-item {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: 4px 10px;
  min-height: 44px;
  padding: 8px 10px;
  border: 1px solid var(--linha);
  border-radius: var(--raio-pequeno);
  background: var(--superficie);
  color: var(--tinta);
  font-size: 0.72rem;
  line-height: 1.45;
}

.upload-item--falhou {
  border-color: var(--acafrao);
}

.upload-nome {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.upload-estado {
  grid-column: 1;
  color: var(--tinta-suave);
  font-weight: 600;
}

.upload-item--falhou .upload-estado {
  /* --acafrao tem ~3:1 sobre --superficie: insuficiente para texto pequeno.
     A borda clay continua sinalizando o erro; o texto fica em tinta. */
  color: var(--tinta);
  font-weight: 700;
}

.upload-barra {
  grid-column: 1 / -1;
  height: 3px;
  overflow: hidden;
  border-radius: 999px;
  background: var(--linha);
}

.upload-barra > span {
  display: block;
  height: 100%;
  background: var(--terracota);
  transform-origin: left center;
  transition: transform 180ms ease;
}

.upload-nota,
.upload-heic {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--tinta);
}

.upload-retry {
  grid-column: 2;
  grid-row: 1 / span 2;
  min-width: 44px;
  min-height: 44px;
  padding: 0 4px;
  border: 0;
  background: none;
  color: var(--terracota);
  font: inherit;
  font-weight: 700;
  text-decoration: underline;
  cursor: pointer;
}

.upload-retry:focus-visible {
  outline: 3px solid var(--terracota);
  outline-offset: 2px;
}

.upload-status {
  flex: 0 0 100%;
  margin: 0;
  color: var(--tinta-suave);
  font-size: 0.72rem;
  line-height: 1.45;
}

@media (prefers-reduced-motion: reduce) {
  .upload-barra > span {
    transition: none;
  }
}
```

- [ ] **Step 2: empilhar a ação no mobile**

Dentro de `@media (max-width: 520px)` (`:3650`), acrescentar:

```css
  .upload-retry {
    grid-column: 1 / -1;
    grid-row: auto;
    width: 100%;
  }
```

- [ ] **Step 3: confirmar que nenhuma cor literal nova entrou**

Run: `cd Projetos/fisga/web && npm run check && npm run build && npx playwright test tests/registrar-upload.spec.ts --reporter=list`
Expected: PASS; inspeção visual rápida do artefato `fisga-produto/registrar/index.html` confirma o mesmo vocabulário (chip, nota de alerta, ação quiet).

- [ ] **Step 4: commit**

```bash
git add Projetos/fisga/web/src/styles/global.css
git commit -m "style(fisga): estados de envio e retry por foto"
```

---

### Task 4: documentação e verificação final

**Files:**
- Modify: `Projetos/fisga/docs/INDEX.md` (índice do plano; a spec já está indexada)

**Interfaces:**
- Consumes: spec e plan deste PR.
- Produces: documento localizável; PR pronto para review.

- [ ] **Step 1: conferir o índice**

`docs/INDEX.md` já referencia a spec e o plano deste PR. Não criar outra entrada.

- [ ] **Step 2: rodar tudo**

```bash
cd Projetos/fisga
export PATH="$HOME/.dotnet:$PATH"
dotnet restore Fisga.slnx --locked-mode
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build
cd web && npm ci && npm run check && npm run build && npm run test:e2e
cd ../../.. && npx --yes markdownlint-cli2@0.24.1
```

Expected: tudo PASS. A API não mudou, mas a CI roda `openapi.json`; não editar o contrato à mão.

- [ ] **Step 3: commit e PR**

```bash
git add Projetos/fisga/docs
git commit -m "docs(fisga): spec e plano do upload com progresso e retry"
git push -u origin issue-52-upload-fotos
gh pr create --fill --body "$(cat <<'EOF'
Closes #52

## O que mudou
- HEIC/HEIF entra no seletor; decode nativo, com nota inline quando o navegador não lê.
- Progresso real por foto (`XMLHttpRequest.upload.onprogress`) e status em `role="status"`.
- Falha isolada com "Tentar de novo" só na foto que não subiu; concluídas nunca são reenviadas.

## Verificação
- [x] `dotnet build`/`dotnet test`
- [x] `npm run check`, `npm run build`, `npm run test:e2e`
- [x] markdownlint

Sem mudança de API, contrato, banco ou configuração. Web-only; rollback é revert.
EOF
)"
```

---

## Notas para o revisor

- O retry não reenvia o lote: `enviarEntrada` recebe uma entrada e `definirFotos` deduplica por `Set`.
- O contador `uploadsPendentes` mudou de "lotes" para "entradas em envio"; por isso `#btn-salvar` e a checagem de salvamento continuam corretos mesmo com várias fotos no ar.
- A limitação do OpenDesign (adaptador `--dir` incompatível com OpenCode v2.3.2 e BYOK sem provider) está registrada na spec; a trilha de design foi feita com o artefato existente, lido antes da spec.
