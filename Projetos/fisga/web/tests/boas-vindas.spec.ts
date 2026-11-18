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
