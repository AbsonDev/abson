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

test('recarregar durante a inicialização não apaga o rascunho salvo', async ({ page }) => {
  const localDistintivo = 'Praia do Rio Novo — rascunho protegido';
  await page.addInitScript(() => {
    if (sessionStorage.getItem('fisga:rascunho-semeado')) return;
    localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
      entrada: {
        local: 'Praia do Rio Novo — rascunho protegido', cidade: 'Ilhabela', estado: 'SP',
        ambiente: 'Mar', data: '2026-11-19', periodo: 'Manha', mare: 'Vazante',
        lua: 'Minguante', clima: 'Nublado', clarezaDaAgua: 'Clara',
        privacidadeDoLocal: 'Exato', observacoes: null, pescador: null,
        capturas: [{ especie: 'Robalo', pesoKg: 2 }]
      },
      atualizadoEm: '2026-11-19T08:00:00Z'
    }));
    sessionStorage.setItem('fisga:rascunho-semeado', '1');
  });
  await mockAnonimoLento(page, 1500);
  await page.goto(`${ORIGEM}/registrar`);
  // Ainda dentro da janela assíncrona: não esperar valores que dependem da hidratação.
  await page.waitForTimeout(150);
  await page.reload();

  const rascunho = await page.evaluate(() => JSON.parse(localStorage.getItem('fisga:rascunho:v1') || 'null'));
  expect(rascunho?.entrada?.local).toBe(localDistintivo);
  expect(rascunho?.entrada?.capturas).toHaveLength(1);
  expect(rascunho?.entrada?.capturas?.[0]?.especie).toBe('Robalo');
});

test('espécie digitada antes do auth não é substituída pela wiki', async ({ page }) => {
  const especieDigitada = 'Robalo digitado antes do auth';
  await page.addInitScript(() => {
    if (sessionStorage.getItem('fisga:rascunho-semeado')) return;
    localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
      entrada: {
        local: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar',
        data: '2026-11-19', periodo: 'Manha', mare: 'Vazante', lua: 'Minguante',
        clima: 'Nublado', clarezaDaAgua: 'Clara', privacidadeDoLocal: 'Exato',
        observacoes: null, pescador: null,
        capturas: [{ especie: 'Robalo', pesoKg: 2 }]
      },
      atualizadoEm: '2026-11-19T08:00:00Z'
    }));
    sessionStorage.setItem('fisga:rascunho-semeado', '1');
  });
  await mockAnonimoLento(page, 1500);
  await page.goto(`${ORIGEM}/registrar?especie=DaWiki`);
  await page.locator('[data-etapa="3"]').evaluate((etapa) => etapa.removeAttribute('hidden'));

  const primeira = page.locator('.captura-especie').first();
  // Sem hidratação síncrona o teste cria a linha para poder digitar antes do auth.
  if (await primeira.count() === 0) await page.locator('#adicionar-captura').click();
  await primeira.fill(especieDigitada);
  await expect(primeira).toHaveValue(especieDigitada);

  await expect(page.locator('#acesso-registrar')).toBeVisible();
  await page.waitForTimeout(200);
  await expect(page.locator('.captura-especie').first()).toHaveValue(especieDigitada);
  await expect(page.locator('.captura-especie')).toHaveCount(1);
});

test('editar captura do rascunho antes do auth não duplica a linha', async ({ page }) => {
  await page.addInitScript(() => {
    if (sessionStorage.getItem('fisga:rascunho-semeado')) return;
    localStorage.setItem('fisga:rascunho:v1', JSON.stringify({
      entrada: {
        local: 'Ilhabela', cidade: 'Ilhabela', estado: 'SP', ambiente: 'Mar',
        data: '2026-11-19', periodo: 'Manha', mare: 'Vazante', lua: 'Minguante',
        clima: 'Nublado', clarezaDaAgua: 'Clara', privacidadeDoLocal: 'Exato',
        observacoes: null, pescador: null,
        capturas: [{ especie: 'Robalo', pesoKg: 2 }]
      },
      atualizadoEm: '2026-11-19T08:00:00Z'
    }));
    sessionStorage.setItem('fisga:rascunho-semeado', '1');
  });
  await mockAnonimoLento(page, 1500);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('[data-etapa="3"]').evaluate((etapa) => etapa.removeAttribute('hidden'));

  const primeira = page.locator('.captura-especie').first();
  await expect(primeira).toHaveValue('Robalo');
  await primeira.fill('Robalo editado');
  await expect(primeira).toHaveValue('Robalo editado');

  await expect(page.locator('#acesso-registrar')).toBeVisible();
  await page.waitForTimeout(200);
  await expect(page.locator('.linha-captura')).toHaveCount(1);
  await expect(page.locator('.captura-especie').first()).toHaveValue('Robalo editado');
});

test('foto anônima mostra nota inline e não redireciona', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  // A foto mora na etapa 3; torná-la visível não altera o estado do formulário.
  await page.locator('[data-etapa="3"]').evaluate((etapa) => etapa.removeAttribute('hidden'));
  await page.locator('.captura-foto-adicionar').click();
  await expect(page.locator('.foto-nota')).toBeVisible();
  await expect(page.locator('.foto-nota')).toContainText('A foto entra depois que você entra.');
  await expect(page).toHaveURL(/\/registrar$/);
  await expect(page.locator('input[type="file"]').first()).toHaveJSProperty('files.length', 0);
});

test('submit anônimo abre confirmação, continua aqui mantém os dados e entrar preserva o volta', async ({ page }) => {
  await mockAnonimo(page);
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  await page.locator('#btn-salvar').click();
  await expect(page.locator('#confirmar-salvar')).toBeVisible();
  await expect(page.locator('#btn-salvar')).toBeHidden();
  await expect(page).toHaveURL(/\/registrar/);

  // Fidelidade ao artefato no mobile: botões empilhados e dentro da largura do painel.
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('#confirmar-salvar').scrollIntoViewIfNeeded();
  const caixaPainel = await page.locator('#confirmar-salvar').boundingBox();
  const caixaEntrar = await page.locator('#confirmar-entrar').boundingBox();
  const caixaContinuar = await page.locator('#confirmar-continuar').boundingBox();
  expect(caixaPainel).not.toBeNull();
  expect(caixaEntrar).not.toBeNull();
  expect(caixaContinuar).not.toBeNull();
  expect(caixaContinuar!.y).toBeGreaterThanOrEqual(caixaEntrar!.y + caixaEntrar!.height);
  for (const caixa of [caixaEntrar!, caixaContinuar!]) {
    expect(caixa.x).toBeGreaterThanOrEqual(caixaPainel!.x);
    expect(caixa.x + caixa.width).toBeLessThanOrEqual(caixaPainel!.x + caixaPainel!.width);
  }
  await page.setViewportSize({ width: 1280, height: 720 });

  await page.locator('#confirmar-continuar').click();
  await expect(page.locator('#confirmar-salvar')).toBeHidden();
  await expect(page.locator('#btn-salvar')).toBeVisible();
  await expect(page.locator('#local')).toHaveValue('Ilhabela');
  await page.locator('#btn-salvar').click();
  await Promise.all([
    page.waitForURL((url) => url.pathname === '/entrar' && Boolean(url.searchParams.get('voltar')?.startsWith('/registrar'))),
    page.locator('#confirmar-entrar').click()
  ]);
});

test('com sessão, salvar segue para a pescaria sem painel de confirmação', async ({ page }) => {
  await page.route(`${API}/api/auth/me`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ autenticado: true, usuario: { id: '1', email: 'p@fisga.app', nome: 'Marina', nomeExibicao: 'Marina' }, consentimentoPendente: false, googleConectado: false, temSenha: true })
  }));
  await page.route(`${API}/api/pescarias`, (route) => route.fulfill({
    status: 200, headers: { ...CORS, 'content-type': 'application/json' },
    body: JSON.stringify({ id: 'nova', shareToken: 'xyz' })
  }));
  await page.goto(`${ORIGEM}/registrar`);
  await page.locator('#local').fill('Ilhabela');
  await page.locator('#data').fill('2026-11-19');
  await page.getByRole('button', { name: /4 Revisar/ }).click();
  await page.locator('#btn-salvar').click();
  await expect(page).toHaveURL(/\/p\/xyz\?novo=1/);
  await expect(page.locator('#confirmar-salvar')).toBeHidden();
});
