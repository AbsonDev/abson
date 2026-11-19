import { test, expect, type Page } from '@playwright/test';

/**
 * Fluxo de captura primeiro, conta depois em /registrar.
 * A API é mockada no navegador; a origem do web é localhost, igual à configuração
 * do Playwright, e o mock de API continua em localhost:5184.
 */

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

test('deslogado abre com formulário ativo, banner compacto e nenhuma primária de conta', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await expect(page.locator('#acesso-registrar')).toContainText('Você pode preencher agora. Entrar só quando quiser salvar.');
  await expect(page.locator('#pescaria-form')).not.toHaveAttribute('inert', /.*/);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /2 As condições/ }).click();
  await expect(page.locator('[data-etapa="2"]')).toBeVisible();
  await expect(page).toHaveURL(/\/registrar$/);
});

test('rascunho anônimo sobrevive a recarregar antes do debounce', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.reload();
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#rascunho-indicador')).toContainText('Rascunho salvo neste aparelho');
});

test('captura com apenas isca, solto e foto volta do rascunho', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: {
      local: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar', data: '2026-11-19',
      periodo: 'Tarde', mare: 'Enchente', lua: 'Crescente', clima: 'Sol', clarezaDaAgua: 'NaoInformada',
      privacidadeDoLocal: 'Exato', observacoes: null, pescador: null,
      capturas: [{ especie: '', pesoKg: 0, comprimentoCm: 0, isca: 'Camarão', solto: true,
        fotoUrl: 'http://localhost:5184/foto.jpg', fotos: ['http://localhost:5184/foto.jpg'], observacao: null }]
    },
    atualizadoEm: '2026-11-19T08:00:00Z'
  })));
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await expect(page.locator('.captura-isca')).toHaveValue('Camarão');
  await expect(page.locator('.captura-solto-check')).toBeChecked();
  await expect(page.locator('.captura-foto-thumb')).toHaveCount(1);
});
