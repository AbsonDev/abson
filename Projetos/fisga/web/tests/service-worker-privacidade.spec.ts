import { test, expect, type Page } from '@playwright/test';

/**
 * Privacidade do service worker e do logout.
 *
 * O SW só registra fora de `localhost` (ver Layout.astro), então estes testes
 * usam `127.0.0.1` como origem. A API continua em `localhost:5184`; os mocks de
 * `page.route` precisam devolver CORS porque a origem do navegador muda.
 */

const PORTA = Number(process.env.PORT ?? 4323);
const ORIGEM = `http://127.0.0.1:${PORTA}`;
const API = 'http://localhost:5184';

const CORS = {
  'access-control-allow-origin': ORIGEM,
  'access-control-allow-credentials': 'true'
};

async function esperarServiceWorker(page: Page) {
  // O Layout só registra em `https:` e fora de `localhost`; o teste registra
  // explicitamente em `127.0.0.1` (origem segura para service workers).
  await page.evaluate(async () => {
    if (!(await navigator.serviceWorker.getRegistration())) {
      await navigator.serviceWorker.register('/sw.js');
    }
    await navigator.serviceWorker.ready;
  });
  await page.waitForFunction(() => navigator.serviceWorker.controller !== null, undefined, {
    timeout: 20_000
  });
}

/**
 * Sessão mockada com estado: depois do `POST /api/auth/sair` o `/api/auth/me`
 * passa a responder anônimo, como aconteceria no backend real. Sem isso a tela
 * de entrada poderia redirecionar de volta para `/hoje` e o teste ficaria
 * dependente do timing da navegação.
 */
async function mockSessao(page: Page) {
  const estado = { autenticado: true };

  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({
        autenticado: estado.autenticado,
        usuario: estado.autenticado
          ? {
              id: 'usuario-1',
              email: 'pescador@fisga.app',
              nome: 'Marina Sá',
              nomeExibicao: 'Marina Sá',
              aceitaNovidades: false
            }
          : null,
        consentimentoPendente: false,
        googleConectado: false,
        temSenha: true
      })
    })
  );

  await page.route(`${API}/api/auth/sair`, (route) => {
    if (route.request().method() === 'OPTIONS') {
      return route.fulfill({
        status: 204,
        headers: {
          ...CORS,
          'access-control-allow-methods': 'GET,POST,OPTIONS',
          'access-control-allow-headers': 'content-type'
        }
      });
    }
    estado.autenticado = false;
    return route.fulfill({ status: 204, headers: CORS, body: '' });
  });
}

/**
 * `/api/auth/me` responde 200 anônimo quando não há sessão (inclusive cookie
 * expirado). É diferente de 401, que só endpoints autenticados devolvem.
 */
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

/**
 * Semeia o estado privado em uma página da origem. Roda antes da navegação
 * para a tela testada, já que o cache/localStorage são da origem e sobrevivem
 * à troca de documento.
 *
 * `fisga-public-*` guarda só conteúdo aprovado por `podeCachear` (offline, wiki,
 * ferramentas); `fisga-private-*` guarda o que precisa sumir no logout/expiração.
 * `fisga:rascunho:v1` é a única chave que pode sobreviver à expiração anônima.
 */
const CHAVES_PRIVADAS = [
  'fisga:rascunho:v1',
  'fisga:locais:v1',
  'fisga:pontos:v1',
  'fisga:trilhas:v1',
  'fisga:sessao:v1'
] as const;

async function semearEstadoPrivado(page: Page) {
  await page.goto(`${ORIGEM}/offline`);
  await page.evaluate(async (chaves) => {
    localStorage.setItem(chaves[0], 'rascunho privado');
    localStorage.setItem(chaves[1], '[]');
    localStorage.setItem(chaves[2], '[]');
    localStorage.setItem(chaves[3], '[]');
    localStorage.setItem(chaves[4], '1');

    const publico = await caches.open('fisga-public-v10');
    await publico.put('/offline', new Response('offline público'));

    const privado = await caches.open('fisga-private-v10');
    await privado.put('/hoje', new Response('html privado'));
    await privado.put('/conta', new Response('html privado'));
  }, CHAVES_PRIVADAS);
}

async function chavesDeCache(page: Page): Promise<string[]> {
  return page.evaluate(() => caches.keys());
}

async function caminhosNoCache(page: Page): Promise<string[]> {
  return page.evaluate(async () => {
    const todos: string[] = [];
    for (const nome of await caches.keys()) {
      const cache = await caches.open(nome);
      for (const requisicao of await cache.keys()) {
        todos.push(new URL(requisicao.url).pathname);
      }
    }
    return todos;
  });
}

/**
 * O cache público e os tiles sobrevivem; o privado e o `localStorage` somem.
 * `preservarRascunho` cobre a exceção das telas anônimas (`/registrar`, `/mapa`).
 */
async function expectPublicoPreservadoEPrivadoRemovido(
  page: Page,
  opcoes: { preservarRascunho?: boolean } = {}
) {
  const chaves = await chavesDeCache(page);
  expect(chaves).toContain('fisga-public-v10');
  expect(chaves.some((chave) => chave.startsWith('fisga-private-'))).toBe(false);

  const caminhos = await caminhosNoCache(page);
  expect(caminhos).toContain('/offline');
  expect(caminhos).not.toContain('/conta');
  expect(caminhos).not.toContain('/hoje');

  const valores = await page.evaluate(
    (nomes) => Object.fromEntries(nomes.map((nome) => [nome, localStorage.getItem(nome)])),
    [...CHAVES_PRIVADAS]
  );

  if (opcoes.preservarRascunho) {
    expect(valores['fisga:rascunho:v1']).toBe('rascunho privado');
  } else {
    expect(valores['fisga:rascunho:v1']).toBeNull();
  }

  for (const chave of CHAVES_PRIVADAS) {
    if (chave === 'fisga:rascunho:v1' && opcoes.preservarRascunho) continue;
    expect(valores[chave]).toBeNull();
  }
}

async function mockEntrar(page: Page) {
  await page.route('**/entrar**', (route) =>
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'text/html', 'cache-control': 'no-store' },
      body: '<!doctype html><h1>Entrar</h1>'
    })
  );
}

async function mockEntrarVoltarConta(page: Page) {
  await page.route('**/entrar?voltar=/conta', (route) =>
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'text/html', 'cache-control': 'no-store' },
      body: '<!doctype html><h1>Entrar</h1>'
    })
  );
}

test.describe('logout sem service worker', () => {
  // O SW ativo recria `fisga-public-v10` com os assets públicos de `/entrar`
  // logo depois do logout. Aqui ele fica bloqueado para medir exatamente o que
  // `limparCachePrivado` apaga; o comportamento com SW ativo é coberto pelo
  // teste de rota privada offline.
  test.use({ serviceWorkers: 'block' });

  test('sair limpa caches privados e chaves fisga: mas preserva o cache público', async ({ page }) => {
    await mockSessao(page);
    await page.goto(`${ORIGEM}/conta`);
    await expect(page.locator('#conta-content')).toBeVisible();

    await page.evaluate(async () => {
      localStorage.setItem('fisga:rascunho:v1', 'rascunho privado');
      localStorage.setItem('fisga:locais:v1', '[]');
      localStorage.setItem('outro:app', 'mantém');

      const publico = await caches.open('fisga-public-v10');
      await publico.put('/offline', new Response('offline público'));
      await publico.put(
        '/ferramentas',
        new Response('página pública', { headers: { 'content-type': 'text/html' } })
      );

      const privado = await caches.open('fisga-private-v10');
      await privado.put('/conta', new Response('html privado'));
      await privado.put('/hoje', new Response('html privado'));

      const antigo = await caches.open('fisga-v8');
      await antigo.put('/meu-diario', new Response('html antigo'));

      const tiles = await caches.open('fisga-tiles-v1');
      await tiles.put('/tile.png', new Response('tile público'));
    });

    await page.route('**/entrar?saiu=1', (route) =>
      route.fulfill({
        status: 200,
        headers: { 'content-type': 'text/html', 'cache-control': 'no-store' },
        body: '<!doctype html><h1>Saiu</h1>'
      })
    );

    await page.locator('#sair-atual').click();
    await expect(page.locator('#dialog-sair')).toBeVisible();
    await page.locator('#sair-confirmar').click();
    await expect(page).toHaveURL(/\/entrar\?saiu=1$/);

    const chaves = await chavesDeCache(page);
    expect(chaves).toContain('fisga-public-v10');
    expect(chaves.some((chave) => chave.startsWith('fisga-private-'))).toBe(false);
    expect(chaves).not.toContain('fisga-v8');
    expect(chaves).toContain('fisga-tiles-v1');

    const caminhos = await caminhosNoCache(page);
    expect(caminhos).toContain('/offline');
    expect(caminhos).toContain('/ferramentas');
    expect(caminhos).not.toContain('/conta');
    expect(caminhos).not.toContain('/meu-diario');
    expect(caminhos).not.toContain('/hoje');

    expect(await page.evaluate(() => localStorage.getItem('fisga:rascunho:v1'))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem('fisga:locais:v1'))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem('outro:app'))).toBe('mantém');
  });

  test('sessão expirada (401) também limpa os caches privados', async ({ page }) => {
    await page.route(`${API}/api/auth/me`, (route) =>
      route.fulfill({ status: 401, headers: CORS, body: '' })
    );
    await mockEntrarVoltarConta(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/conta`);
    await expect(page).toHaveURL(/\/entrar\?voltar=\/conta$/);

    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('sessão expirada (200 anônimo) também limpa os caches privados', async ({ page }) => {
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
    await mockEntrarVoltarConta(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/conta`);
    await expect(page).toHaveURL(/\/entrar\?voltar=\/conta$/);

    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('meu-diario anônimo limpa estado privado e manda para entrar', async ({ page }) => {
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
    await mockEntrar(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/meu-diario`);

    await expect(page).toHaveURL(
      (url) => url.pathname === '/entrar' && url.searchParams.get('voltar') === '/meu-diario'
    );
    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('meu-diario com 401 limpa estado privado e manda para entrar', async ({ page }) => {
    await page.route(`${API}/api/auth/me`, (route) =>
      route.fulfill({ status: 401, headers: CORS, body: '' })
    );
    await mockEntrar(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/meu-diario`);

    await expect(page).toHaveURL(
      (url) => url.pathname === '/entrar' && url.searchParams.get('voltar') === '/meu-diario'
    );
    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('mapa com 401 limpa estado privado e manda para entrar', async ({ page }) => {
    await page.route(`${API}/api/auth/me`, (route) =>
      route.fulfill({ status: 401, headers: CORS, body: '' })
    );
    await mockEntrar(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/mapa`);

    await expect(page).toHaveURL(
      (url) => url.pathname === '/entrar' && url.searchParams.get('voltar') === '/mapa'
    );
    await expectPublicoPreservadoEPrivadoRemovido(page, { preservarRascunho: true });
  });

  test('mapa anônimo limpa estado privado sem tirar o mapa do ar', async ({ page }) => {
    await mockAnonimo(page);
    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/mapa`);

    await expect(page).toHaveURL((url) => url.pathname === '/mapa');
    await expect(page.locator('#mapa')).toBeVisible();
    await expectPublicoPreservadoEPrivadoRemovido(page, { preservarRascunho: true });
  });

  test('registrar anônimo preserva o rascunho e mantém o formulário ativo', async ({ page }) => {
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
    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/registrar`);

    await expect(page.locator('#acesso-registrar')).toBeVisible();
    await expect(page.locator('#pescaria-form')).not.toHaveAttribute('inert', /.*/);
    await expectPublicoPreservadoEPrivadoRemovido(page, { preservarRascunho: true });
  });

  test('registrar com 401 limpa o rascunho e manda para entrar', async ({ page }) => {
    await page.route(`${API}/api/auth/me`, (route) =>
      route.fulfill({ status: 401, headers: CORS, body: '' })
    );
    await mockEntrar(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/registrar`);

    await expect(page).toHaveURL(
      (url) => url.pathname === '/entrar' && url.searchParams.get('voltar') === '/registrar'
    );
    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('sair do menu em /registrar apaga o rascunho mesmo com gravação pendente', async ({ page }) => {
    await mockSessao(page);
    await mockEntrar(page);
    await page.goto(`${ORIGEM}/registrar`);
    await page.locator('#local').fill('Ilhabela');
    await expect
      .poll(() => page.evaluate(() => localStorage.getItem('fisga:rascunho:v1')))
      .not.toBeNull();

    await page.locator('[data-conta-gatilho]').click();
    await page.locator('[data-conta-sair]').click();

    await expect(page).toHaveURL(/\/entrar\?saiu=1$/);
    expect(await page.evaluate(() => localStorage.getItem('fisga:rascunho:v1'))).toBeNull();
  });

  test('registrar ?editar= com 401 limpa o rascunho e manda para entrar', async ({ page }) => {
    await mockSessao(page);
    await page.route(`${API}/api/pescarias/editar-401/editar`, (route) =>
      route.fulfill({ status: 401, headers: CORS, body: '' })
    );
    await mockEntrar(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/registrar?editar=editar-401`);

    await expect(page).toHaveURL(
      (url) => url.pathname === '/entrar'
        && url.searchParams.get('voltar') === '/registrar?editar=editar-401'
    );
    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('boas-vindas anônimo limpa estado privado e manda para hoje', async ({ page }) => {
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
    await mockEntrar(page);

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/boas-vindas`);

    await expect(page).toHaveURL(
      (url) => url.pathname === '/entrar' && url.searchParams.get('voltar') === '/hoje'
    );
    await expectPublicoPreservadoEPrivadoRemovido(page);
  });

  test('aceite com consentimento 401 limpa caches e mostra sessão expirada', async ({ page }) => {
    await page.route(`${API}/api/legal/versoes`, (route) =>
      route.fulfill({
        status: 200,
        headers: { ...CORS, 'content-type': 'application/json' },
        body: JSON.stringify({
          termosVersao: '2026-11-17',
          privacidadeVersao: '2026-11-17'
        })
      })
    );

    await page.route(`${API}/api/conta/consentimento`, (route) => {
      if (route.request().method() === 'OPTIONS') {
        return route.fulfill({
          status: 204,
          headers: {
            ...CORS,
            'access-control-allow-methods': 'POST,OPTIONS',
            'access-control-allow-headers': 'content-type'
          }
        });
      }
      return route.fulfill({
        status: 401,
        headers: { ...CORS, 'content-type': 'application/problem+json' },
        body: JSON.stringify({
          codigo: 'sessao_expirada',
          detalhe: 'Sua sessão expirou. Entre novamente para aceitar.'
        })
      });
    });

    await semearEstadoPrivado(page);
    await page.goto(`${ORIGEM}/aceite`);

    await page.locator('#accept-terms').check();
    await page.locator('#accept-privacy').check();
    await page.locator('#accept-submit').click();

    await expect(page.locator('#accept-error')).toBeVisible();
    await expect(page.locator('#accept-error')).toContainText('Sua sessão expirou');

    await expectPublicoPreservadoEPrivadoRemovido(page);
  });
});

const MARCADOR_PRIVADO = 'conteudo-privado-da-sessao';

test('install precacheia as páginas públicas de acesso e offline', async ({ page }) => {
  await page.goto(`${ORIGEM}/offline`);
  await esperarServiceWorker(page);

  const caminhos = await caminhosNoCache(page);
  // `/entrar` e `/offline` são SSR: só entram no precache porque o middleware
  // responde `public`; o default-deny de `podeCachear` descartaria um HTML 2xx
  // sem header.
  expect(caminhos).toContain('/entrar');
  expect(caminhos).toContain('/offline');
});

test('rota privada nunca é servida do cache quando offline', async ({ page, context }) => {
  await page.goto(`${ORIGEM}/offline`);
  await esperarServiceWorker(page);

  await page.evaluate(async (marcador) => {
    const cache = await caches.open('fisga-public-v10');
    await cache.put(
      '/hoje',
      new Response(`<!doctype html><h1>${marcador}</h1>`, {
        headers: { 'content-type': 'text/html' }
      })
    );
  }, MARCADOR_PRIVADO);

  await context.setOffline(true);
  await page.goto(`${ORIGEM}/hoje`);

  await expect(page.locator('body')).toContainText('O peixe pode esperar');
  await expect(page.locator('body')).not.toContainText(MARCADOR_PRIVADO);
});
