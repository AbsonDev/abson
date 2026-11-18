import { test, expect, devices, type Browser, type BrowserContext, type Page } from '@playwright/test';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

/**
 * E2E da migração de identidade com backend REAL (Task 13).
 *
 * Cobre: cadastro por senha → confirmação → consentimento → app; usuário
 * existente por link mágico → consentimento → diário com dados preservados;
 * redefinição de senha; sessões ("sair desta" e "sair de todos"); `/conta`
 * (senha, perfil/newsletter, exportação JSON) e Google indisponível.
 *
 * Requisitos:
 * - API real em E2E_API_URL (padrão http://localhost:5184), Development, SQLite
 *   descartável, `Email:Provider=Outbox` e E2E_OUTBOX apontando para a pasta.
 * - Web em E2E_WEB_URL/baseURL.
 * - `scripts/e2e-real.sh` prepara tudo e roda esta suíte.
 *
 * A suíte padrão (mockada) ignora este arquivo no playwright.config.ts.
 */

const API = (process.env.E2E_API_URL ?? 'http://localhost:5184').replace(/\/$/, '');
const OUTBOX = process.env.E2E_OUTBOX ?? '';
const REAL = process.env.E2E_REAL === '1';

const SENHA = 'senha-e2e-123';
const SENHA_NOVA = 'senha-e2e-nova-456';

test.describe.configure({ mode: 'serial' });
test.skip(!REAL, 'Defina E2E_REAL=1 e rode via scripts/e2e-real.sh (backend real).');

type EmailCapturado = {
  destino: string;
  assunto: string;
  html?: string | null;
  texto: string;
  criadoEm?: string;
};

function emailUnico(prefixo: string) {
  return `${prefixo}-${Date.now()}-${Math.floor(Math.random() * 1_000_000)}@exemplo.com`;
}

/** Lê o outbox do backend real e espera o e-mail mais recente para o destino. */
async function emailPara(destino: string): Promise<EmailCapturado> {
  if (!OUTBOX) {
    throw new Error('E2E_OUTBOX não configurado; rode via scripts/e2e-real.sh.');
  }

  let encontrado: EmailCapturado | undefined;

  await expect
    .poll(
      async () => {
        const arquivos = await readdir(OUTBOX).catch(() => [] as string[]);
        const registros: EmailCapturado[] = [];

        for (const arquivo of arquivos.filter((nome) => nome.endsWith('.json')).sort().reverse()) {
          try {
            registros.push(JSON.parse(await readFile(path.join(OUTBOX, arquivo), 'utf8')));
          } catch {
            // Arquivo ainda sendo escrito: tenta no próximo poll.
          }
        }

        encontrado = registros.find((registro) => registro.destino === destino);
        return Boolean(encontrado);
      },
      { timeout: 15_000, message: `E-mail para ${destino} não apareceu no outbox.` }
    )
    .toBe(true);

  return encontrado!;
}

function linkDoEmail(email: EmailCapturado, caminho: string): string {
  const url = email.texto.match(/https?:\/\/[^\s"'<>]+/)?.[0];
  expect(url, `E-mail "${email.assunto}" sem URL.`).toBeTruthy();
  expect(url).toContain(caminho);
  return url!;
}

async function registrarPelaTela(page: Page, nome: string, email: string, senha: string) {
  await page.goto('/criar-conta');
  await page.locator('#signup-name').fill(nome);
  await page.locator('#signup-email').fill(email);
  await page.locator('#signup-password').fill(senha);
  await page.locator('#signup-submit').click();
  await expect(page.locator('#signup-success')).toBeVisible();
}

async function entrar(page: Page, email: string, senha: string) {
  await page.goto('/entrar');
  await page.locator('#login-email').fill(email);
  await page.locator('#login-password').fill(senha);
  await page.locator('#login-submit').click();

  const erro = page.locator('#login-error');
  const aceite = page.waitForURL(/\/aceite\?destino=/, { timeout: 20_000 }).catch(() => null);
  const hoje = page.waitForURL(/\/hoje$/, { timeout: 20_000 }).catch(() => null);
  const resultado = await Promise.race([aceite, hoje]);

  if (await erro.isVisible()) {
    throw new Error(`Login falhou: ${await erro.innerText()}`);
  }

  expect(resultado, 'Login não redirecionou para /aceite nem /hoje.').not.toBeNull();
}

async function aceitarConsentimento(page: Page) {
  await expect(page.getByRole('heading', { name: 'Antes de continuar.' })).toBeVisible();
  await page.locator('#accept-terms').check();
  await page.locator('#accept-privacy').check();
  const resposta = page.waitForResponse(
    (res) => res.url() === `${API}/api/conta/consentimento` && res.request().method() === 'POST'
  );
  await page.locator('#accept-submit').click();
  expect((await resposta).status()).toBe(204);
}

async function pedirLinkMagico(page: Page, email: string): Promise<string> {
  await page.goto('/entrar');
  await page.locator('#login-email').fill(email);
  const resposta = page.waitForResponse(
    (res) => res.url() === `${API}/api/auth/solicitar` && res.request().method() === 'POST'
  );
  await page.locator('#magic-btn').click();
  const corpo = (await (await resposta).json()) as { linkDesenvolvimento?: string | null };

  expect(corpo.linkDesenvolvimento, 'API não devolveu linkDesenvolvimento em Development.').toBeTruthy();
  return corpo.linkDesenvolvimento!;
}

async function confirmarEmail(page: Page, email: string) {
  const capturado = await emailPara(email);
  await page.goto(linkDoEmail(capturado, '/confirmar-email'));
  await expect(page.locator('#confirm-success')).toBeVisible({ timeout: 20_000 });
}

async function criarContexto(browser: Browser): Promise<BrowserContext> {
  return browser.newContext({ baseURL: process.env.E2E_WEB_URL ?? 'http://localhost:4323', ...devices['Desktop Chrome'] });
}

async function sessaoAutenticada(context: BrowserContext): Promise<boolean> {
  const resposta = await context.request.get(`${API}/api/auth/me`);
  const corpo = (await resposta.json()) as { autenticado?: boolean };
  return corpo.autenticado === true;
}

test.describe('Identity E2E — backend real', () => {
  test('novo usuário por senha: confirmação, consentimento e app', async ({ page }) => {
    const email = emailUnico('senha');

    await registrarPelaTela(page, 'Pescador E2E', email, SENHA);
    await confirmarEmail(page, email);

    await page.getByRole('link', { name: 'Entrar' }).click();
    await expect(page).toHaveURL(/\/entrar$/);

    await entrar(page, email, SENHA);
    await expect(page).toHaveURL(/\/aceite\?destino=/);

    await aceitarConsentimento(page);
    await expect(page).toHaveURL(/\/hoje$/, { timeout: 20_000 });

    await page.goto('/diario');
    await expect(page).toHaveURL(/\/meu-diario$/);
    await expect(page.locator('#conteudo-diario')).toBeVisible();

    const me = await page.request.get(`${API}/api/auth/me`);
    const corpo = (await me.json()) as { autenticado: boolean; consentimentoPendente: boolean };
    expect(corpo.autenticado).toBe(true);
    expect(corpo.consentimentoPendente).toBe(false);
  });

  test('usuário existente por link mágico: consentimento, diário e dados preservados', async ({ page }) => {
    const email = emailUnico('magico');

    await page.goto('/entrar');
    await page.evaluate(() => sessionStorage.setItem('fisga:retorno', '/diario'));

    const primeiroLink = await pedirLinkMagico(page, email);
    await page.goto(primeiroLink);

    // Primeiro acesso: boas-vindas detecta o aceite pendente e manda para /aceite.
    await expect(page).toHaveURL(/\/aceite\?destino=/, { timeout: 20_000 });
    await aceitarConsentimento(page);
    await expect(page).toHaveURL(/\/boas-vindas/);
    await page.locator('#boas-pular').click();
    await expect(page).toHaveURL(/\/meu-diario$/, { timeout: 20_000 });

    const pescaria = await page.request.post(`${API}/api/pescarias`, {
      data: {
        local: 'Praia E2E',
        pescador: 'Pescador E2E',
        cidade: 'Aracaju',
        estado: 'SE',
        ambiente: 'Mar',
        data: '2026-11-10',
        periodo: 'Manha',
        mare: 'Vazante',
        lua: 'Crescente',
        clima: 'Sol',
        clarezaDaAgua: 'Clara',
        observacoes: 'Pescaria criada pelo E2E de identidade.',
        privacidadeDoLocal: 'Cidade',
        capturas: [
          {
            especie: 'Robalo',
            pesoKg: 1.2,
            comprimentoCm: 45,
            isca: 'Camarao',
            solto: true,
            observacao: null,
            fotos: []
          }
        ]
      }
    });
    expect(pescaria.status()).toBe(201);

    const saiu = await page.request.post(`${API}/api/auth/sair`);
    expect(saiu.status()).toBe(204);

    // Segunda entrada: usuário antigo, sem novo consentimento, cai no diário.
    await page.goto('/entrar');
    await page.evaluate(() => sessionStorage.setItem('fisga:retorno', '/diario'));
    const segundoLink = await pedirLinkMagico(page, email);
    await page.goto(segundoLink);

    await expect(page).toHaveURL(/\/meu-diario$/, { timeout: 25_000 });
    await expect(page.locator('#stat-pescarias')).toHaveText('1');
    await expect(page.locator('#lista')).toContainText('Praia E2E');
  });

  test('redefinição de senha troca a credencial e invalida a antiga', async ({ page }) => {
    const email = emailUnico('reset');

    await registrarPelaTela(page, 'Reset E2E', email, SENHA);
    await confirmarEmail(page, email);

    await page.goto('/esqueci-senha');
    await page.locator('#forgot-email').fill(email);
    await page.locator('#forgot-submit').click();
    await expect(page.locator('#forgot-response')).toBeVisible();

    const capturado = await emailPara(email);
    await page.goto(linkDoEmail(capturado, '/redefinir-senha'));

    await page.locator('#reset-new').fill(SENHA_NOVA);
    await page.locator('#reset-confirm').fill(SENHA_NOVA);
    await page.locator('#reset-submit').click();
    await expect(page.locator('#reset-success-view')).toBeVisible({ timeout: 20_000 });

    // Senha antiga deixa de funcionar.
    await page.goto('/entrar');
    await page.locator('#login-email').fill(email);
    await page.locator('#login-password').fill(SENHA);
    await page.locator('#login-submit').click();
    await expect(page.locator('#login-error')).toBeVisible();

    // Senha nova entra e exige consentimento (primeiro acesso lógico).
    await entrar(page, email, SENHA_NOVA);
    await expect(page).toHaveURL(/\/aceite\?destino=/);
    await aceitarConsentimento(page);
    await expect(page).toHaveURL(/\/hoje$/, { timeout: 20_000 });
  });

  test('sessões: sair desta sessão e sair de todos', async ({ browser }) => {
    const email = emailUnico('sessoes');
    const setup = await browser.newContext({ baseURL: process.env.E2E_WEB_URL ?? 'http://localhost:4323' });

    try {
      const resposta = await setup.request.post(`${API}/api/auth/registrar`, {
        data: { nome: 'Sessões E2E', email, senha: SENHA }
      });
      expect(resposta.status()).toBe(201);

      const confirmacao = await emailPara(email);
      const confirmar = await setup.request.post(`${API}/api/auth/confirmar-email`, {
        data: {
          uid: new URL(linkDoEmail(confirmacao, '/confirmar-email')).searchParams.get('uid'),
          token: new URL(linkDoEmail(confirmacao, '/confirmar-email')).searchParams.get('token')
        }
      });
      expect(confirmar.status()).toBe(200);
    } finally {
      await setup.close();
    }

    const contextoA = await criarContexto(browser);
    const contextoB = await criarContexto(browser);

    try {
      const paginaA = await contextoA.newPage();
      const paginaB = await contextoB.newPage();

      await entrar(paginaA, email, SENHA);
      await expect(paginaA).toHaveURL(/\/aceite\?destino=/);
      await aceitarConsentimento(paginaA);
      await expect(paginaA).toHaveURL(/\/hoje$/);

      await entrar(paginaB, email, SENHA);
      await expect(paginaB).toHaveURL(/\/hoje$/);

      await paginaA.goto('/conta');
      await expect(paginaA.locator('#conta-content')).toBeVisible();
      await expect(paginaA.locator('#sessoes-lista .session')).toHaveCount(2);

      await paginaA.locator('#sair-atual').click();
      await expect(paginaA.locator('#dialog-sair')).toBeVisible();
      await paginaA.locator('#sair-confirmar').click();
      await expect(paginaA).toHaveURL(/\/entrar\?saiu=1$/, { timeout: 20_000 });

      expect(await sessaoAutenticada(contextoA)).toBe(false);
      expect(await sessaoAutenticada(contextoB)).toBe(true);

      // Volta com uma sessão nova e encerra todas.
      await entrar(paginaA, email, SENHA);
      await expect(paginaA).toHaveURL(/\/hoje$/);
      await paginaA.goto('/conta');
      await expect(paginaA.locator('#conta-content')).toBeVisible();

      await paginaA.locator('#sair-todos').click();
      await expect(paginaA.locator('#dialog-sair')).toBeVisible();
      await paginaA.locator('#sair-confirmar').click();
      await expect(paginaA).toHaveURL(/\/entrar\?saiu=1$/, { timeout: 20_000 });

      await expect
        .poll(async () => sessaoAutenticada(contextoB), { timeout: 10_000 })
        .toBe(false);
    } finally {
      await contextoA.close();
      await contextoB.close();
    }
  });

  test('/conta: criar e alterar senha, perfil, newsletter e exportação JSON', async ({ page }) => {
    const email = emailUnico('conta');

    const link = await pedirLinkMagico(page, email);
    await page.goto(link);
    await expect(page).toHaveURL(/\/aceite\?destino=/, { timeout: 20_000 });
    await aceitarConsentimento(page);
    await expect(page).toHaveURL(/\/boas-vindas/);
    await page.locator('#boas-pular').click();
    await expect(page).toHaveURL(/\/hoje$/, { timeout: 20_000 });

    await page.goto('/conta');
    await expect(page.locator('#conta-content')).toBeVisible();

    // Google sem credenciais: status visível, sem botão de conectar.
    await expect(page.locator('#google-status')).toHaveText('Não conectado');
    await expect(page.getByRole('button', { name: 'Conectar Google' })).toHaveCount(0);
    await expect(page.locator('#google-indisponivel')).toBeVisible();

    // Cria a primeira senha (conta nasceu por link mágico).
    await expect(page.locator('#conta-senha-acao')).toHaveText('Criar senha');
    await page.locator('#conta-senha-acao').click();
    await expect(page.locator('#dialog-senha')).toBeVisible();
    await expect(page.locator('#campo-senha-atual')).toBeHidden();
    // O diálogo agenda o foco no próximo frame; esperar o foco evita que ele
    // roube o cursor no meio do preenchimento.
    await expect(page.locator('#nova-senha')).toBeFocused();
    await page.locator('#nova-senha').fill(SENHA);
    await page.locator('#confirmar-senha').fill(SENHA);
    await page.locator('#senha-salvar').click();
    await expect(page.locator('#dialog-senha')).toBeHidden();
    await expect(page.locator('#senha-feedback')).toContainText('Senha criada');
    await expect(page.locator('#conta-senha-acao')).toHaveText('Alterar senha');

    // Altera a senha e continua autenticado (cookie reemitido).
    await page.locator('#conta-senha-acao').click();
    await expect(page.locator('#senha-atual')).toBeFocused();
    await page.locator('#senha-atual').fill(SENHA);
    await page.locator('#nova-senha').fill(SENHA_NOVA);
    await page.locator('#confirmar-senha').fill(SENHA_NOVA);
    await page.locator('#senha-salvar').click();
    await expect(page.locator('#senha-feedback')).toContainText('Senha alterada');
    expect(await sessaoAutenticada(page.context())).toBe(true);

    // Perfil e newsletter.
    await page.locator('#dados-nome').fill('Marina E2E');
    await page.locator('#dados-novidades').check();
    await page.locator('#dados-salvar').click();
    await expect(page.locator('#dados-feedback')).toContainText('Dados salvos');

    const me = await page.request.get(`${API}/api/auth/me`);
    const corpoMe = (await me.json()) as { usuario: { nome: string; aceitaNovidades: boolean } };
    expect(corpoMe.usuario.nome).toBe('Marina E2E');
    expect(corpoMe.usuario.aceitaNovidades).toBe(true);

    // Exportação JSON da conta.
    const download = await Promise.all([
      page.waitForEvent('download'),
      page.locator('#dados-exportar').click()
    ]).then(([evento]) => evento);
    expect(download.suggestedFilename()).toMatch(/^fisga-conta-\d{8}\.json$/);
    const caminho = await download.path();
    const exportado = JSON.parse(await readFile(caminho!, 'utf8')) as {
      usuario: { email: string };
      pescarias: unknown[];
    };
    expect(exportado.usuario.email).toBe(email);
    expect(Array.isArray(exportado.pescarias)).toBe(true);

    // A senha nova funciona depois de sair.
    const saiu = await page.request.post(`${API}/api/auth/sair`);
    expect(saiu.status()).toBe(204);
    await entrar(page, email, SENHA_NOVA);
    await expect(page).toHaveURL(/\/hoje$/);
  });

  test('Google indisponível: endpoint false e botão oculto', async ({ page, request }) => {
    const resposta = await request.get(`${API}/api/auth/google/disponivel`);
    expect(resposta.status()).toBe(200);
    expect((await resposta.json()) as { disponivel: boolean }).toEqual({ disponivel: false });

    await page.goto('/entrar');
    await expect(page.locator('#google-login')).toBeHidden();
    await expect(page.locator('#login-divider')).toBeHidden();
  });
});
