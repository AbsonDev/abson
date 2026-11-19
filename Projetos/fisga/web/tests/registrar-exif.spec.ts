import { test, expect, type Page } from '@playwright/test';
import { FOTO_COM_EXIF_BASE64, fotoSemExif } from './fixtures/foto-exif';

const PORTA = Number(process.env.PORT ?? 4323);
const ORIGEM = `http://localhost:${PORTA}`;
const API = 'http://localhost:5184';

const CORS = {
  'access-control-allow-origin': ORIGEM,
  'access-control-allow-credentials': 'true'
};

// Padrão copiado de registrar-anonimo.spec.ts. O upload de foto exige sessão:
// com `autenticado: false` o arquivo é descartado antes de aplicarExif().
async function mockAutenticado(page: Page) {
  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({
        autenticado: true,
        usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' },
        consentimentoPendente: false,
        googleConectado: false,
        temSenha: true
      })
    })
  );
}

async function mockCondicoes(page: Page) {
  await page.route(`${API}/api/condicoes**`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({
        local: { nome: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar' },
        temMare: true,
        mare: { estado: 'subindo', proximoEvento: null },
        solunar: { fase: 'Crescente', faseNome: 'crescente' },
        tempo: { ventoKmh: 12, chuvaProbabilidade: 20 },
        atualizadoEm: '2026-11-19T08:00:00Z'
      })
    })
  );
}

async function prepararUpload(page: Page) {
  await mockAutenticado(page);
  await mockCondicoes(page);
  await page.route(`${API}/api/locais/reverso**`, (route) =>
    route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({ cidade: 'Ilhabela', estado: 'SP' })
    })
  );
  await page.route(`${API}/api/midia**`, (route) =>
    route.fulfill({ status: 201, headers: CORS, contentType: 'application/json', body: '{}' })
  );
}

async function enviarFotoComExif(page: Page, nome = 'foto.jpg') {
  await page.locator('input[type="file"]').first().setInputFiles({
    name: nome,
    mimeType: 'image/jpeg',
    buffer: Buffer.from(FOTO_COM_EXIF_BASE64, 'base64')
  });
  await expect(page.locator('#exif-aviso')).toBeVisible();
}

test('foto com EXIF mostra aviso com Aplicar e Ignorar sem mudar nenhum campo', async ({ page }) => {
  await prepararUpload(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#data').fill('2026-11-20');
  await page.locator('#periodo').selectOption('Noite');
  await page.locator('#local').fill('Praia do Meio');
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'foto.jpg',
    mimeType: 'image/jpeg',
    buffer: Buffer.from(FOTO_COM_EXIF_BASE64, 'base64')
  });

  await expect(page.locator('#exif-aviso')).toBeVisible();
  await expect(page.locator('#exif-texto')).toHaveText(
    'Encontramos data, horário e cidade na foto. Só preenchemos campos que estão vazios — o local exato nunca vem da foto.'
  );
  await expect(page.locator('#exif-valores')).toContainText('Ilhabela');
  await expect(page.getByRole('button', { name: /Aplicar nos campos vazios/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ignorar' })).toBeVisible();
  await expect(page.locator('#data')).toHaveValue('2026-11-20');
  await expect(page.locator('#periodo')).toHaveValue('Noite');
  await expect(page.locator('#local')).toHaveValue('Praia do Meio');
  await expect(page.locator('#cidade')).toHaveValue('');
  await expect(page.locator('#estado')).toHaveValue('');
});

test('aviso de EXIF empilha ações no mobile e mantém alvos de 44 px', async ({ page }) => {
  await prepararUpload(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'foto.jpg',
    mimeType: 'image/jpeg',
    buffer: Buffer.from(FOTO_COM_EXIF_BASE64, 'base64')
  });
  await expect(page.locator('#exif-aviso')).toBeVisible();

  const aplicar = page.locator('#exif-aplicar');
  const ignorar = page.locator('#exif-ignorar');
  for (const botao of [aplicar, ignorar]) {
    const caixa = await botao.boundingBox();
    expect(caixa).not.toBeNull();
    expect(caixa!.height).toBeGreaterThanOrEqual(44);
  }

  const desktopAplicar = (await aplicar.boundingBox())!;
  const desktopIgnorar = (await ignorar.boundingBox())!;
  expect(desktopIgnorar.y).toBeCloseTo(desktopAplicar.y, 0);

  await page.setViewportSize({ width: 390, height: 844 });
  const mobileAplicar = (await aplicar.boundingBox())!;
  const mobileIgnorar = (await ignorar.boundingBox())!;
  expect(mobileIgnorar.y).toBeGreaterThanOrEqual(mobileAplicar.y + mobileAplicar.height);
});

test('foto sem dados não abre aviso e mantém o fluxo', async ({ page }) => {
  await prepararUpload(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'sem-exif.jpg',
    mimeType: 'image/jpeg',
    buffer: fotoSemExif()
  });

  await expect(page.locator('#exif-aviso')).toBeHidden();
  await expect(page.locator('input[type="file"]').first()).toBeEnabled();
});

test('Aplicar preenche data, período, cidade e estado vazios, nunca o local', async ({ page }) => {
  await prepararUpload(page);
  await page.goto(`${ORIGEM}/registrar`);
  // O formulário chega com data de hoje e período padrão: limpar para exercitar "vazio".
  await page.locator('#data').fill('');
  await page.locator('#periodo').evaluate((elemento) => {
    (elemento as HTMLSelectElement).value = '';
  });

  await enviarFotoComExif(page);
  await expect(page.locator('#exif-valores')).toHaveText('18/11/2026, 06:42 · Ilhabela, SP');
  await expect(page.locator('#data')).toHaveValue('');
  await expect(page.locator('#periodo')).toHaveValue('');

  await page.getByRole('button', { name: /Aplicar nos campos vazios/ }).click();

  await expect(page.locator('#data')).toHaveValue('2026-11-18');
  await expect(page.locator('#periodo')).toHaveValue('Manha');
  await expect(page.locator('#cidade')).toHaveValue('Ilhabela');
  await expect(page.locator('#estado')).toHaveValue('SP');
  await expect(page.locator('#local')).toHaveValue('');
  await expect(page.locator('#exif-aviso')).toBeHidden();
  await expect(page.locator('#exif-estado')).toHaveText('Campos vazios preenchidos pela foto.');
  await expect(page.locator('#local')).toBeFocused();
});

test('Aplicar não sobrescreve campo digitado durante o upload', async ({ page }) => {
  await prepararUpload(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#data').fill('2026-11-19');
  await page.locator('#periodo').selectOption('Noite');

  await enviarFotoComExif(page);
  await page.getByRole('button', { name: /Aplicar nos campos vazios/ }).click();

  await expect(page.locator('#data')).toHaveValue('2026-11-19');
  await expect(page.locator('#periodo')).toHaveValue('Noite');
  await expect(page.locator('#cidade')).toHaveValue('Ilhabela');
  await expect(page.locator('#exif-estado')).toHaveText('Campos vazios preenchidos pela foto.');
});

test('reverso indisponível ainda mostra data e aplica só o que veio da foto', async ({ page }) => {
  await prepararUpload(page);
  // Registrada depois do mock de sucesso: a última route vale neste teste.
  await page.route(`${API}/api/locais/reverso**`, (route) => route.abort());
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#data').fill('');

  await enviarFotoComExif(page);
  await expect(page.locator('#exif-valores')).toHaveText('18/11/2026, 06:42');
  await page.getByRole('button', { name: /Aplicar nos campos vazios/ }).click();

  await expect(page.locator('#data')).toHaveValue('2026-11-18');
  await expect(page.locator('#cidade')).toHaveValue('');
  await expect(page.locator('#estado')).toHaveValue('');
});
