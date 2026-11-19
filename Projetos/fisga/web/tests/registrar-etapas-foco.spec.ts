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

async function abrirEtapa2(page: Page) {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /2 As condições/ }).click();
}

test('avançar para a etapa 2 foca o título com o prefixo da etapa', async ({ page }) => {
  await abrirEtapa2(page);
  await expect(page.locator('[data-etapa="2"]')).toBeVisible();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-condicoes');
  await expect(page.locator('#titulo-condicoes')).toHaveAccessibleName('Etapa 2 — As condições');
});

test('voltar foca o título da etapa anterior', async ({ page }) => {
  await abrirEtapa2(page);
  await page.locator('[data-etapa="2"] [data-voltar]').click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-pescaria');
  await expect(page.locator('#titulo-pescaria')).toHaveAccessibleName('Etapa 1 — A pescaria');
});

test('clique na navegação foca o título de cada etapa', async ({ page }) => {
  await abrirEtapa2(page);
  await page.getByRole('button', { name: /3 As capturas/ }).click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-capturas');
  await expect(page.locator('#titulo-capturas')).toHaveAccessibleName('Etapa 3 — As capturas');
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('titulo-revisar');
  await expect(page.locator('#titulo-revisar')).toHaveAccessibleName('Etapa 4 — Revisar');
});

test('validação com local vazio mantém o foco no campo', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('');
  await page.getByRole('button', { name: /2 As condições/ }).click();
  expect(await page.evaluate(() => document.activeElement?.id)).toBe('local');
});

test('carga inicial não rouba foco', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  expect(await page.evaluate(() => document.activeElement?.tagName)).toBe('BODY');
});
