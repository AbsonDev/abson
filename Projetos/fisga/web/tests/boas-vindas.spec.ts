import { test, expect, type Page } from '@playwright/test';

/**
 * Suíte da tela de boas-vindas em uma pergunta.
 *
 * O fluxo é 100% mockado com `page.route`: a tela busca a sessão no cliente e
 * usa a API de locais ao escolher o lugar. O que importa aqui é o contrato da
 * tela — saudação, ausência do wizard antigo, guards e o payload salvo.
 */

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
        {
          nome: 'Praia de Aruana',
          cidade: 'Aracaju',
          estado: 'SE',
          latitude: '-10.95',
          longitude: '-37.05'
        }
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

test('geolocalização aceita salva e navega', async ({ page }) => {
  await mockMe(page, USUARIO);
  const corpo = await mockLocais(page);
  await mockPagina(page, '/hoje');
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: {
        getCurrentPosition: (ok: PositionCallback) =>
          ok({
            coords: { latitude: -10.911234, longitude: -37.071234 }
          } as GeolocationPosition)
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
      value: {
        getCurrentPosition: (_ok: PositionCallback, erro: PositionErrorCallback) =>
          erro(new Error('denied') as unknown as GeolocationPositionError)
      }
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
