import { test, expect, type Page } from '@playwright/test';

/**
 * Suíte das telas públicas de autenticação (Task 11).
 *
 * As respostas da API são interceptadas com `page.route` para que o fluxo seja
 * determinístico e não dependa de PostgreSQL, SMTP ou credenciais do Google.
 */

const API = 'http://localhost:5184';

function url(caminho: string, query = '') {
  return `/${caminho}${query}`;
}

async function mockJson(page: Page, caminho: string, corpo: unknown, status = 200) {
  await page.route(`${API}${caminho}`, (route) =>
    route.fulfill({
      status,
      contentType: 'application/json',
      body: JSON.stringify(corpo)
    })
  );
}

async function mockGoogle(page: Page, disponivel: boolean) {
  await page.route(`${API}/api/auth/google/disponivel`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ disponivel })
    })
  );
}

/**
 * `/hoje` é renderizado no servidor e consulta `/api/auth/me` com o cookie real.
 * No teste não há cookie da API, então interceptamos o documento para conferir
 * apenas para onde o cliente navegou.
 */
async function mockDestinoHoje(page: Page) {
  await page.route('**/hoje', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><h1>Hoje</h1>' })
  );
}

async function mockMe(page: Page, extras: Record<string, unknown> = {}) {
  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        autenticado: true,
        usuario: { id: '1', email: 'pescador@fisga.app', nomeExibicao: 'Marina Sá' },
        consentimentoPendente: false,
        googleConectado: false,
        ...extras
      })
    })
  );
}

test.beforeEach(async ({ page }) => {
  await mockGoogle(page, false);
  await page.addInitScript(() => {
    try {
      sessionStorage.clear();
    } catch {
      // armazenamento pode estar indisponível em contexto restrito
    }
  });
});

test.describe('Entrar', () => {
  test('botão do Google aparece somente quando disponível', async ({ page }) => {
    await mockGoogle(page, true);
    await page.goto(url('entrar'));
    await expect(page.getByRole('button', { name: 'Continuar com Google' })).toBeVisible();
  });

  test('botão do Google fica oculto quando indisponível', async ({ page }) => {
    await page.goto(url('entrar'));
    await expect(page.getByRole('button', { name: 'Continuar com Google' })).toBeHidden();
    await expect(page.getByRole('heading', { name: /Seu diário está/ })).toBeVisible();
  });

  test('login por senha com sucesso leva ao destino', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockJson(page, '/api/auth/entrar', { autenticado: true, mensagem: null });
    await mockMe(page);
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
  });

  test('login por senha com credenciais inválidas mostra erro', async ({ page }) => {
    await mockJson(page, '/api/auth/entrar', {
      autenticado: false,
      mensagem: 'E-mail ou senha incorretos.'
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('senha-errada');
    await page.locator('#login-submit').click();
    await expect(page.locator('#login-error')).toBeVisible();
    await expect(page.locator('#login-error')).toContainText('E-mail ou senha incorretos');
  });

  test('login com e-mail não confirmado mostra reenvio', async ({ page }) => {
    await mockJson(page, '/api/auth/entrar', {
      autenticado: false,
      mensagem: 'Confirme seu e-mail para entrar. Enviamos um link de confirmação.'
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('nao-confirmado@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page.locator('#login-unconfirmed')).toBeVisible();
    await expect(page.locator('#login-resend')).toBeVisible();
  });

  test('link mágico envia e confirma o e-mail na tela', async ({ page }) => {
    await mockJson(page, '/api/auth/solicitar', {
      mensagem: 'Enviamos um link de acesso.',
      linkDesenvolvimento: null
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#magic-btn').click();
    await expect(page.locator('#magic-note')).toBeVisible();
    await expect(page.locator('#magic-note')).toContainText('pescador@fisga.app');
  });

  test('erro genérico da API é exibido sem detalhes sensíveis', async ({ page }) => {
    await mockJson(page, '/api/auth/entrar', {
      autenticado: false,
      mensagem: 'Não foi possível entrar agora.'
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page.locator('#login-error')).toContainText('Não foi possível entrar agora.');
  });
});

test.describe('Criar conta', () => {
  test('cadastro por senha mostra confirmação', async ({ page }) => {
    await mockJson(page, '/api/auth/registrar', { mensagem: 'Conta criada.' }, 201);
    await page.goto(url('criar-conta'));
    await page.locator('#signup-name').fill('Marina Sá');
    await page.locator('#signup-email').fill('nova@fisga.app');
    await page.locator('#signup-password').fill('senha-de-dez-caracteres');
    await page.locator('#signup-submit').click();
    await expect(page.locator('#signup-success')).toBeVisible();
    await expect(page.locator('#signup-email-echo')).toHaveText('nova@fisga.app');
  });

  test('e-mail já em uso mostra aviso', async ({ page }) => {
    await mockJson(
      page,
      '/api/auth/registrar',
      { codigo: 'email_duplicado', detalhe: 'Este e-mail já tem uma conta.' },
      400
    );
    await page.goto(url('criar-conta'));
    await page.locator('#signup-name').fill('Marina Sá');
    await page.locator('#signup-email').fill('emuso@fisga.app');
    await page.locator('#signup-password').fill('senha-de-dez-caracteres');
    await page.locator('#signup-submit').click();
    await expect(page.locator('#signup-inuse')).toBeVisible();
    await expect(page.locator('#signup-email-error')).toContainText('já tem uma conta');
  });
});

test.describe('Esqueci e redefinir senha', () => {
  test('esqueci a senha mostra estado de sucesso', async ({ page }) => {
    await mockJson(page, '/api/auth/esqueci-senha', { mensagem: 'Enviamos um link.' }, 202);
    await page.goto(url('esqueci-senha'));
    await page.locator('#forgot-email').fill('pescador@fisga.app');
    await page.locator('#forgot-submit').click();
    await expect(page.locator('#forgot-response')).toBeVisible();
    await expect(page.locator('#forgot-response')).toContainText('Confira sua caixa de entrada');
  });

  test('redefinir senha com sucesso', async ({ page }) => {
    await mockJson(page, '/api/auth/redefinir-senha', { mensagem: 'Senha redefinida.' });
    await page.goto(url('redefinir-senha', '?uid=1&token=abc'));
    await page.locator('#reset-new').fill('minha-nova-senha');
    await page.locator('#reset-confirm').fill('minha-nova-senha');
    await page.locator('#reset-submit').click();
    await expect(page.locator('#reset-success-view')).toBeVisible();
  });

  test('link de redefinição inválido mostra estado expirado', async ({ page }) => {
    await mockJson(
      page,
      '/api/auth/redefinir-senha',
      { codigo: 'link_invalido', detalhe: 'Este link expirou.' },
      400
    );
    await page.goto(url('redefinir-senha', '?uid=1&token=expirado'));
    await page.locator('#reset-new').fill('minha-nova-senha');
    await page.locator('#reset-confirm').fill('minha-nova-senha');
    await page.locator('#reset-submit').click();
    await expect(page.locator('#reset-expired-view')).toBeVisible();
  });
});

test.describe('Confirmação de e-mail', () => {
  test('confirmação válida', async ({ page }) => {
    await mockJson(page, '/api/auth/confirmar-email', { mensagem: 'E-mail confirmado.' });
    await page.goto(url('confirmar-email', '?uid=1&token=abc'));
    await expect(page.locator('#confirm-success')).toBeVisible({ timeout: 10000 });
  });

  test('confirmação inválida permite pedir novo link', async ({ page }) => {
    await mockJson(
      page,
      '/api/auth/confirmar-email',
      { codigo: 'confirmacao_invalida', detalhe: 'Link inválido ou expirado.' },
      400
    );
    await page.goto(url('confirmar-email', '?uid=1&token=ruim'));
    await expect(page.locator('#confirm-fail')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('#confirm-resend')).toBeVisible();
  });
});

test.describe('Aceite de termos', () => {
  test('botão fica desabilitado até os dois aceites', async ({ page }) => {
    await page.goto(url('aceite', '?destino=%2Fhoje'));
    const submit = page.locator('#accept-submit');
    await expect(submit).toBeDisabled();
    await page.locator('#accept-terms').check();
    await expect(submit).toBeDisabled();
    await page.locator('#accept-privacy').check();
    await expect(submit).toBeEnabled();
  });

  test('aceite registra as versões vigentes e retorna ao destino', async ({ page }) => {
    await mockDestinoHoje(page);
    let corpo: unknown = null;
    await page.route(`${API}/api/conta/consentimento`, async (route) => {
      const requisicao = route.request();
      corpo = JSON.parse(requisicao.postData() || '{}');
      await route.fulfill({ status: 204, body: '' });
    });
    await page.goto(url('aceite', '?destino=%2Fhoje'));
    await page.locator('#accept-terms').check();
    await page.locator('#accept-privacy').check();
    await page.locator('#accept-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
    expect(corpo).toEqual({ termosVersao: '2026-11-17', privacidadeVersao: '2026-11-17' });
  });
});

test.describe('Gate de consentimento', () => {
  test('página autenticada com consentimento pendente redireciona para /aceite', async ({ page }) => {
    await mockMe(page, { consentimentoPendente: true });
    await page.goto(url('meu-diario'));
    await expect(page).toHaveURL(/\/aceite\?destino=%2Fmeu-diario/);
  });
});
