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
  await expect(page.locator('input[type="file"]').first()).toHaveAttribute('accept', /image\/heic/);
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
  await input.setInputFiles({ name: 'falha.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('quebrada') });
  await expect(page.locator('.upload-item--falhou')).toBeVisible();
  await input.setInputFiles({ name: 'outra.jpg', mimeType: 'image/jpeg', buffer: PNG });
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
  await expect(page.locator('.upload-item--falhou')).toHaveCount(1);
});
