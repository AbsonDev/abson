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

test.describe('logout sem service worker', () => {
  // O SW ativo recria `fisga-public-v9` com os assets públicos de `/entrar`
  // logo depois do logout. Aqui ele fica bloqueado para medir exatamente o que
  // `limparCachePrivado` apaga; o comportamento com SW ativo é coberto pelo
  // teste de rota privada offline.
  test.use({ serviceWorkers: 'block' });

  test('sair limpa caches fisga e chaves fisga: do localStorage', async ({ page }) => {
    await mockSessao(page);
    await page.goto(`${ORIGEM}/conta`);
    await expect(page.locator('#conta-content')).toBeVisible();

    await page.evaluate(async () => {
      localStorage.setItem('fisga:diario:v1', 'rascunho privado');
      localStorage.setItem('fisga:locais:v1', '[]');
      localStorage.setItem('outro:app', 'mantém');

      const publico = await caches.open('fisga-public-v9');
      await publico.put(
        '/conta',
        new Response('html privado', { headers: { 'content-type': 'text/html' } })
      );

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

    const chaves = await page.evaluate(() => caches.keys());
    expect(chaves.some((c) => c.startsWith('fisga-public-'))).toBe(false);
    expect(chaves).not.toContain('fisga-v8');
    expect(chaves).toContain('fisga-tiles-v1');

    const caminhos = await page.evaluate(async () => {
      const todos: string[] = [];
      for (const nome of await caches.keys()) {
        const cache = await caches.open(nome);
        for (const requisicao of await cache.keys()) {
          todos.push(new URL(requisicao.url).pathname);
        }
      }
      return todos;
    });
    expect(caminhos).not.toContain('/conta');
    expect(caminhos).not.toContain('/meu-diario');

    expect(await page.evaluate(() => localStorage.getItem('fisga:diario:v1'))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem('fisga:locais:v1'))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem('outro:app'))).toBe('mantém');
  });

  test('sessão expirada (401) também limpa os caches privados', async ({ page }) => {
    await page.route(`${API}/api/auth/me`, (route) =>
      route.fulfill({ status: 401, headers: CORS, body: '' })
    );
    await page.route('**/entrar?voltar=/conta', (route) =>
      route.fulfill({
        status: 200,
        headers: { 'content-type': 'text/html', 'cache-control': 'no-store' },
        body: '<!doctype html><h1>Entrar</h1>'
      })
    );

    await page.goto(`${ORIGEM}/offline`);
    await page.evaluate(async () => {
      localStorage.setItem('fisga:diario:v1', 'rascunho privado');
      const cache = await caches.open('fisga-public-v9');
      await cache.put('/hoje', new Response('html privado'));
    });

    await page.goto(`${ORIGEM}/conta`);
    await expect(page).toHaveURL(/\/entrar\?voltar=\/conta$/);

    const chaves = await page.evaluate(() => caches.keys());
    expect(chaves.some((c) => c.startsWith('fisga-public-'))).toBe(false);
    expect(await page.evaluate(() => localStorage.getItem('fisga:diario:v1'))).toBeNull();
  });
});

const MARCADOR_PRIVADO = 'conteudo-privado-da-sessao';

test('rota privada nunca é servida do cache quando offline', async ({ page, context }) => {
  await page.goto(`${ORIGEM}/offline`);
  await esperarServiceWorker(page);

  await page.evaluate(async (marcador) => {
    const cache = await caches.open('fisga-public-v9');
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
