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

async function mockSessaoAutenticada(page: Page) {
  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({
        autenticado: true,
        usuario: {
          id: 'usuario-1',
          email: 'pescador@fisga.app',
          nome: 'Marina Sá',
          nomeExibicao: 'Marina Sá',
          aceitaNovidades: false
        },
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
    return route.fulfill({ status: 204, headers: CORS, body: '' });
  });
}

test('sair limpa caches fisga e chaves fisga: do localStorage', async ({ page }) => {
  await mockSessaoAutenticada(page);
  await page.goto(`${ORIGEM}/conta`);
  await expect(page.locator('#conta-content')).toBeVisible();
  await esperarServiceWorker(page);

  await page.evaluate(async () => {
    localStorage.setItem('fisga:diario:v1', 'rascunho privado');
    localStorage.setItem('fisga:locais:v1', '[]');
    localStorage.setItem('outro:app', 'mantém');

    const cache = await caches.open('fisga-public-v9');
    await cache.put(
      '/conta',
      new Response('html privado', { headers: { 'content-type': 'text/html' } })
    );
  });

  // A página pós-logout é mockada com `no-store` como salvaguarda: se o teste
  // rodar sem service worker ativo, o roteamento do Playwright ainda impede
  // que uma navegação crie um cache público novo.
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
  // O cache público pode ser recriado pelos assets hasheados da tela de
  // entrada (`public`), então a verificação é sobre HTML privado: o que o
  // logout apaga é a sessão, não a possibilidade de cachear assets públicos.
  expect(chaves).not.toContain('fisga-v8');

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
  expect(caminhos).not.toContain('/hoje');
  expect(caminhos).not.toContain('/meu-diario');

  expect(await page.evaluate(() => localStorage.getItem('fisga:diario:v1'))).toBeNull();
  expect(await page.evaluate(() => localStorage.getItem('fisga:locais:v1'))).toBeNull();
  expect(await page.evaluate(() => localStorage.getItem('outro:app'))).toBe('mantém');
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
