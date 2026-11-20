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
