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

async function mockAnonimoLento(page: Page, atrasoMs: number) {
  await page.route(`${API}/api/auth/me`, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, atrasoMs));
    await route.fulfill({
      status: 200,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify({
        autenticado: false,
        usuario: null,
        consentimentoPendente: false,
        googleConectado: false,
        temSenha: false
      })
    });
  });
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

async function mockCondicoesLentas(page: Page, atrasoMs: number) {
  await page.route(`${API}/api/condicoes**`, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, atrasoMs));
    await route.fulfill({
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
    });
  });
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

test('?editar= não é sobrescrito por rascunho nem condições da URL', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Rascunho velho', capturas: [] }, atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await page.route(`${API}/api/pescarias/editar-1/editar`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ id: 'editar-1', shareToken: 'abc', entrada: {
      local: 'Salvo no diário', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar',
      data: '2026-10-10', periodo: 'Manha', mare: 'Vazante', lua: 'Cheia', clima: 'Nublado',
      clarezaDaAgua: 'NaoInformada', privacidadeDoLocal: 'Exato', observacoes: null, capturas: []
    } })
  }));
  await mockCondicoes(page);
  await page.route(`${API}/api/auth/me`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ autenticado: true, usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' }, consentimentoPendente: false, googleConectado: false, temSenha: true })
  }));
  await page.goto(`${ORIGEM}/registrar?editar=editar-1&lat=-23.9&lon=-46.3`);
  await expect(page.locator('#local')).toHaveValue('Salvo no diário');
  await expect(page.locator('#condicoes-aplicadas')).toBeHidden();
});

test('digitação antes da resposta das condições não é sobrescrita', async ({ page }) => {
  await mockAnonimo(page);
  await mockCondicoesLentas(page, 600);
  await page.goto(`${ORIGEM}/registrar?lat=-23.9&lon=-46.3&nome=Ilhabela`);
  // O campo fica na etapa 4; torná-la visível não altera o estado do formulário.
  await page.locator('[data-etapa="4"]').evaluate((etapa) => etapa.removeAttribute('hidden'));
  await page.locator('#observacoes').fill('Anotação digitada primeiro');
  await expect(page.locator('#observacoes')).toHaveValue('Anotação digitada primeiro');
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#condicoes-aplicadas')).toBeVisible();
});

test('condições do link vencem rascunho antigo e preservam o resto', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Local antigo', cidade: 'Aracaju', estado: 'SE', ambiente: 'Represa',
      data: '2026-01-01', periodo: 'Manha', mare: 'Vazante', lua: 'Minguante', clima: 'Chuva',
      clarezaDaAgua: 'Turva', privacidadeDoLocal: 'Aproximada', observacoes: 'Anotação antiga',
      pescador: 'Marina', capturas: [{ especie: 'Robalo', pesoKg: 2, capturasExtra: 'não usa' }] },
    atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await mockAnonimo(page);
  await mockCondicoes(page);
  await page.goto(`${ORIGEM}/registrar?lat=-23.9&lon=-46.3&nome=Ilhabela&cidade=Ilhabela&estado=SP&ambiente=Mar`);
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#mare')).toHaveValue('Enchente');
  await expect(page.locator('#lua')).toHaveValue('Crescente');
  await expect(page.locator('#clima')).toHaveValue('Sol');
  await expect(page.locator('#observacoes')).toHaveValue('Anotação antiga');
  await expect(page.locator('.captura-especie').first()).toHaveValue('Robalo');
  await expect(page.locator('#condicoes-texto')).toContainText('Os valores do link vieram na frente do seu rascunho');
});

test('depois do login, voltar com lat/lon reaplica condições e mostra o banner', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Antigo', data: '2026-01-01', capturas: [] }, atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await page.route(`${API}/api/auth/me`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ autenticado: true, usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' }, consentimentoPendente: false, googleConectado: false, temSenha: true })
  }));
  await mockCondicoes(page);
  await page.goto(`${ORIGEM}/registrar?lat=-23.9&lon=-46.3&nome=Ilhabela`);
  await expect(page.locator('#acesso-registrar')).toBeHidden();
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#condicoes-aplicadas')).toBeVisible();
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  await expect(page.locator('#btn-salvar')).toBeVisible();
});

test('espécie da wiki não conta como toque e mantém a precedência do rascunho', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
    entrada: { local: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar', data: '2026-11-19',
      periodo: 'Manha', mare: 'Vazante', lua: 'Minguante', clima: 'Nublado', clarezaDaAgua: 'Clara',
      privacidadeDoLocal: 'Exato', observacoes: 'Anotação antiga', pescador: 'Marina',
      capturas: [{ especie: 'Robalo', pesoKg: 2 }] },
    atualizadoEm: '2026-01-01T00:00:00Z'
  })));
  await mockAnonimo(page);
  await mockCondicoes(page);
  await page.goto(`${ORIGEM}/registrar?especie=Camurim&lat=-23.9&lon=-46.3&nome=Ilhabela`);
  await expect(page.locator('.captura-especie').first()).toHaveValue('Camurim');
  await expect(page.locator('.captura-especie')).toHaveCount(1);
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await expect(page.locator('#observacoes')).toHaveValue('Anotação antiga');
  await expect(page.locator('#clareza')).toHaveValue('Clara');
});

test('data digitada antes do auth não é sobrescrita na inicialização vazia', async ({ page }) => {
  // O cenário é o do formulário vazio: o autosave não pode criar rascunho.
  await page.addInitScript(() => {
    const gravar = Storage.prototype.setItem;
    Storage.prototype.setItem = function (this: Storage, chave: string, valor: string) {
      if (chave === 'fisga:rascunho:v1') return;
      gravar.call(this, chave, valor);
    };
  });
  await mockAnonimoLento(page, 1000);
  await page.goto(`${ORIGEM}/registrar`);
  await expect(page.locator('#acesso-registrar')).toBeHidden();
  await page.locator('#data').fill('2001-02-03');
  await expect(page.locator('#acesso-registrar')).toBeHidden();
  await expect(page.locator('#data')).toHaveValue('2001-02-03');
  await expect(page.locator('#contador-capturas')).toHaveText('1 captura');
  await expect(page.locator('#data')).toHaveValue('2001-02-03');
});
