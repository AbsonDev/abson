import { test, expect, type Page } from '@playwright/test';

/**
 * Suíte das telas públicas de autenticação (Task 11).
 *
 * As respostas da API são interceptadas com `page.route` para que o fluxo seja
 * determinístico e não dependa de PostgreSQL, SMTP ou credenciais do Google.
 * A exceção é `/hoje`, renderizado no servidor: ali o cookie
 * `fisga_e2e_pendente=1` é lido pela API mínima de `tests/mock-api.mjs`.
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

/**
 * `/entrar` consulta `/me` ao carregar (para tirar quem já tem sessão do
 * formulário) e de novo depois do login. A primeira chamada é anônima; a
 * segunda devolve a sessão descrita em `depois`.
 */
async function mockMeAntesEDepoisDoLogin(page: Page, depois: Record<string, unknown> = {}) {
  let chamadas = 0;
  await page.route(`${API}/api/auth/me`, async (route) => {
    chamadas += 1;
    const autenticado = chamadas > 1;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(
        autenticado
          ? {
              autenticado: true,
              usuario: { id: '1', email: 'pescador@fisga.app', nomeExibicao: 'Marina Sá' },
              consentimentoPendente: false,
              googleConectado: false,
              ...depois
            }
          : {
              autenticado: false,
              usuario: null,
              consentimentoPendente: false,
              googleConectado: false
            }
      )
    });
  });
}

test.beforeEach(async ({ page }) => {
  await mockGoogle(page, false);
  await mockMe(page, { autenticado: false, usuario: null, consentimentoPendente: false });
  await page.addInitScript(() => {
    try {
      sessionStorage.clear();
    } catch {
      // armazenamento pode estar indisponível em contexto restrito
    }
  });
});

test.describe('Entrar', () => {
  test('botão do Google aparece somente quando disponível, com o divisor', async ({ page }) => {
    await mockGoogle(page, true);
    await page.goto(url('entrar'));
    await expect(page.getByRole('button', { name: 'Continuar com Google' })).toBeVisible();
    await expect(page.locator('#login-divider')).toBeVisible();
  });

  test('botão do Google fica oculto quando indisponível, sem divisor órfão', async ({ page }) => {
    await page.goto(url('entrar'));
    await expect(page.getByRole('button', { name: 'Continuar com Google' })).toBeHidden();
    await expect(page.locator('#login-divider')).toBeHidden();
    await expect(page.getByRole('heading', { name: /Seu diário está/ })).toBeVisible();
  });

  test('sessão com aceite pendente vai para o aceite', async ({ page }) => {
    await mockMe(page, { consentimentoPendente: true });
    await page.goto(url('entrar'));
    await expect(page).toHaveURL(/\/aceite\?destino=%2Fhoje/);
  });

  test('sessão com aceite vai direto ao diário', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockMe(page);
    await page.goto(url('entrar'));
    await expect(page).toHaveURL(/\/hoje/);
  });

  test('login por senha com sucesso leva ao destino', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockJson(page, '/api/auth/entrar', { autenticado: true, mensagem: null });
    await mockMeAntesEDepoisDoLogin(page);
    const checagemInicial = page.waitForResponse(`${API}/api/auth/me`);
    await page.goto(url('entrar'));
    await checagemInicial;
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
  });

  test('login com consentimento pendente leva ao aceite', async ({ page }) => {
    await mockJson(page, '/api/auth/entrar', { autenticado: true, mensagem: null });
    await mockMeAntesEDepoisDoLogin(page, { consentimentoPendente: true });
    const checagemInicial = page.waitForResponse(`${API}/api/auth/me`);
    await page.goto(url('entrar'));
    await checagemInicial;
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page).toHaveURL(/\/aceite\?destino=%2Fhoje/);
    await expect(page.getByRole('heading', { name: 'Antes de continuar.' })).toBeVisible();
  });

  test('voltar externo é ignorado e mantém o destino interno', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockJson(page, '/api/auth/entrar', { autenticado: true, mensagem: null });
    await mockMeAntesEDepoisDoLogin(page);
    const checagemInicial = page.waitForResponse(`${API}/api/auth/me`);
    await page.goto(url('entrar', '?voltar=%2F%5Cevil.example'));
    await checagemInicial;
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
  });

  test('destino explícito limpa o retorno salvo', async ({ page }) => {
    await page.route('**/conta', (route) =>
      route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><h1>Conta</h1>' })
    );
    await mockJson(page, '/api/auth/entrar', { autenticado: true, mensagem: null });
    await mockMeAntesEDepoisDoLogin(page);
    const checagemInicial = page.waitForResponse(`${API}/api/auth/me`);
    await page.goto(url('entrar', '?destino=%2Fconta'));
    await checagemInicial;
    await page.evaluate(() => sessionStorage.setItem('fisga:retorno', '/mapa'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await expect(page).toHaveURL(/\/conta$/);
    expect(await page.evaluate(() => sessionStorage.getItem('fisga:retorno'))).toBeNull();
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
    await expect(page.locator('#login-error-title')).toHaveText('E-mail ou senha incorretos.');
    await expect(page.locator('#login-error')).toContainText('E-mail ou senha incorretos');
  });

  test('bloqueio temporário usa título neutro', async ({ page }) => {
    await mockJson(page, '/api/auth/entrar', {
      autenticado: false,
      mensagem: 'Muitas tentativas. Tente novamente em alguns minutos.'
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#login-password').fill('senha-errada');
    await page.locator('#login-submit').click();
    await expect(page.locator('#login-error-title')).toHaveText('Não foi possível continuar.');
    await expect(page.locator('#login-error')).toContainText('Muitas tentativas');
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

  test('reenvio restaura a nota de sucesso depois de uma falha', async ({ page }) => {
    await mockJson(page, '/api/auth/entrar', {
      autenticado: false,
      mensagem: 'Confirme seu e-mail para entrar. Enviamos um link de confirmação.'
    });
    let chamadas = 0;
    await page.route(`${API}/api/auth/solicitar`, (route) => {
      chamadas += 1;
      if (chamadas === 1) {
        return route.fulfill({
          status: 500,
          contentType: 'application/problem+json',
          body: JSON.stringify({ codigo: 'falha', detalhe: 'Não foi possível enviar.' })
        });
      }
      return route.fulfill({
        status: 202,
        contentType: 'application/json',
        body: JSON.stringify({ mensagem: 'Link enviado.' })
      });
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('nao-confirmado@fisga.app');
    await page.locator('#login-password').fill('uma-senha-qualquer');
    await page.locator('#login-submit').click();
    await page.locator('#login-resend').click();
    await expect(page.locator('#login-resend-note')).toContainText('Não foi possível enviar');
    await page.locator('#login-resend').click();
    await expect(page.locator('#login-resend-note')).toContainText('Novo link enviado');
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

  test('falha do link mágico usa título neutro e esconde a nota anterior', async ({ page }) => {
    let chamadas = 0;
    await page.route(`${API}/api/auth/solicitar`, (route) => {
      chamadas += 1;
      if (chamadas === 1) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ mensagem: 'Enviamos um link de acesso.' })
        });
      }
      return route.fulfill({
        status: 500,
        contentType: 'application/problem+json',
        body: JSON.stringify({ codigo: 'falha', detalhe: 'Não foi possível enviar o link agora.' })
      });
    });
    await page.goto(url('entrar'));
    await page.locator('#login-email').fill('pescador@fisga.app');
    await page.locator('#magic-btn').click();
    await expect(page.locator('#magic-note')).toBeVisible();
    await page.locator('#magic-btn').click();
    await expect(page.locator('#magic-note')).toBeHidden();
    await expect(page.locator('#login-error-title')).toHaveText('Não foi possível continuar.');
    await expect(page.locator('#login-error')).toContainText('Não foi possível enviar o link agora.');
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
      { codigo: 'email_em_uso', detalhe: 'Este e-mail já tem uma conta.' },
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

  test('falha temporária mantém o formulário e mostra erro geral', async ({ page }) => {
    await mockJson(
      page,
      '/api/auth/redefinir-senha',
      { codigo: 'muitas_tentativas', detalhe: 'Muitas tentativas. Tente em alguns minutos.' },
      429
    );
    await page.goto(url('redefinir-senha', '?uid=1&token=abc'));
    await page.locator('#reset-new').fill('minha-nova-senha');
    await page.locator('#reset-confirm').fill('minha-nova-senha');
    await page.locator('#reset-submit').click();
    await expect(page.locator('#reset-form-view')).toBeVisible();
    await expect(page.locator('#reset-expired-view')).toBeHidden();
    await expect(page.locator('#reset-error')).toBeVisible();
    await expect(page.locator('#reset-error')).toContainText('Muitas tentativas');
  });
});

test.describe('Confirmação de e-mail', () => {
  test('confirmação válida', async ({ page }) => {
    await mockJson(page, '/api/auth/confirmar-email', { mensagem: 'E-mail confirmado.' });
    await page.goto(url('confirmar-email', '?uid=1&token=abc'));
    await expect(page.locator('#confirm-success')).toBeVisible({ timeout: 10000 });
  });

  test('limpa uid e token da URL e envia Referrer-Policy no-referrer', async ({ page }) => {
    await mockJson(page, '/api/auth/confirmar-email', { mensagem: 'E-mail confirmado.' });

    await page.goto(url('confirmar-email', '?uid=1&token=segredo'));
    await expect(page.locator('#confirm-success')).toBeVisible({ timeout: 10000 });

    await expect(page).toHaveURL(/\/confirmar-email$/);
    await expect(page.locator('meta[name="referrer"]')).toHaveAttribute('content', 'no-referrer');

    await page.goto(url('redefinir-senha', '?uid=1&token=outro-segredo'));
    await expect(page).toHaveURL(/\/redefinir-senha$/);
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

  test('sucesso oferece uma única ação para entrar', async ({ page }) => {
    await mockJson(page, '/api/auth/confirmar-email', { mensagem: 'E-mail confirmado.' });
    await page.goto(url('confirmar-email', '?uid=1&token=abc'));
    await expect(page.locator('#confirm-success')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('#confirm-success').getByRole('link', { name: 'Entrar' })).toHaveAttribute(
      'href',
      '/entrar'
    );
    await expect(page.locator('#confirm-success').getByRole('link')).toHaveCount(1);
  });

  test('pedir novo link sem sessão explica o passo de entrar', async ({ page }) => {
    await mockJson(
      page,
      '/api/auth/confirmar-email',
      { codigo: 'confirmacao_invalida', detalhe: 'Link inválido ou expirado.' },
      400
    );
    await page.goto(url('confirmar-email', '?uid=1&token=ruim'));
    await expect(page.locator('#confirm-fail')).toBeVisible({ timeout: 10000 });
    await page.locator('#confirm-resend').click();
    await expect(page.locator('#confirm-resend-note')).toBeVisible();
    await expect(page.locator('#confirm-resend-note')).toContainText('entre com seu e-mail e senha');
    await expect(page.locator('#confirm-resend-note [data-icon="erro"]')).toBeVisible();
    await expect(page.locator('#confirm-resend-note [data-icon="ok"]')).toBeHidden();
    await expect(page).toHaveURL(/\/confirmar-email/);
  });
});

test.describe('Confirmação de link de acesso', () => {
  test('link válido leva ao destino interno', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockMe(page);
    await page.addInitScript(() => sessionStorage.setItem('fisga:retorno', '/hoje'));
    await page.goto(url('entrar/confirmar', '?estado=ok'));
    await expect(page).toHaveURL(/\/hoje/);
  });

  test('link expirado mostra o estado de erro', async ({ page }) => {
    await page.goto(url('entrar/confirmar', '?estado=link_expirado'));
    await expect(page.locator('#confirmar-titulo')).toContainText('expirou');
    await expect(page.locator('#confirmar-acao')).toHaveAttribute('href', '/entrar');
    await expect(page.locator('#confirmar-secundaria')).toBeVisible();
  });

  test('sessão com aceite pendente passa pelo aceite', async ({ page }) => {
    await mockMe(page, { consentimentoPendente: true });
    await page.goto(url('entrar/confirmar', '?estado=ok'));
    await expect(page).toHaveURL(/\/aceite\?destino=%2Fhoje/);
  });
});

test.describe('Aceite de termos', () => {
  test('botão fica desabilitado até os dois aceites, com estilo visível', async ({ page }) => {
    await page.goto(url('aceite', '?destino=%2Fhoje'));
    const submit = page.locator('#accept-submit');
    await expect(submit).toBeDisabled();
    await expect(submit).toHaveCSS('opacity', '0.55');
    await page.locator('#accept-terms').check();
    await expect(submit).toBeDisabled();
    await page.locator('#accept-privacy').check();
    await expect(submit).toBeEnabled();
    await expect(submit).toHaveCSS('opacity', '1');
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

  test('aceite busca as versões vigentes e envia exatamente elas', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockJson(page, '/api/legal/versoes', {
      termosVersao: '2030-01-02',
      privacidadeVersao: '2030-02-03'
    });
    let corpo: unknown = null;
    await page.route(`${API}/api/conta/consentimento`, async (route) => {
      corpo = JSON.parse(route.request().postData() || '{}');
      await route.fulfill({ status: 204, body: '' });
    });

    await page.goto(url('aceite', '?destino=%2Fhoje'));
    await expect(page.locator('#aceite-versoes')).toContainText('2030-01-02');
    await expect(page.locator('#aceite-versoes')).toContainText('2030-02-03');

    await page.locator('#accept-terms').check();
    await page.locator('#accept-privacy').check();
    await page.locator('#accept-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
    expect(corpo).toEqual({ termosVersao: '2030-01-02', privacidadeVersao: '2030-02-03' });
  });

  test('aceite usa as versões atuais como fallback quando a API falha', async ({ page }) => {
    await mockDestinoHoje(page);
    await mockJson(
      page,
      '/api/legal/versoes',
      { codigo: 'indisponivel', detalhe: 'Sem rede.' },
      500
    );
    let corpo: unknown = null;
    await page.route(`${API}/api/conta/consentimento`, async (route) => {
      corpo = JSON.parse(route.request().postData() || '{}');
      await route.fulfill({ status: 204, body: '' });
    });

    await page.goto(url('aceite', '?destino=%2Fhoje'));
    await expect(page.locator('#aceite-versoes')).toContainText('2026-11-17');
    await page.locator('#accept-terms').check();
    await page.locator('#accept-privacy').check();
    await page.locator('#accept-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
    expect(corpo).toEqual({ termosVersao: '2026-11-17', privacidadeVersao: '2026-11-17' });
  });

  test('sair encerra a sessão antes de voltar para entrar', async ({ page }) => {
    let chamadas = 0;
    await page.route(`${API}/api/auth/sair`, (route) => {
      chamadas += 1;
      return route.fulfill({ status: 204, body: '' });
    });
    await page.goto(url('entrar?saiu=1'));
    await expect(page.locator('#entrar-aviso')).toBeVisible();
    await page.goto(url('aceite', '?destino=%2Fhoje'));
    await page.locator('#accept-logout').click();
    await expect(page).toHaveURL(/\/entrar\?saiu=1/);
    expect(chamadas).toBe(1);
  });

  test('destino externo no aceite é ignorado', async ({ page }) => {
    await mockDestinoHoje(page);
    await page.route(`${API}/api/conta/consentimento`, (route) =>
      route.fulfill({ status: 204, body: '' })
    );
    await page.goto(url('aceite', '?destino=%2F%5Cevil.example'));
    await page.locator('#accept-terms').check();
    await page.locator('#accept-privacy').check();
    await page.locator('#accept-submit').click();
    await expect(page).toHaveURL(/\/hoje/);
  });
});

test.describe('Gate de consentimento', () => {
  for (const pagina of ['mapa', 'registrar', 'conta', 'boas-vindas', 'meu-diario']) {
    test(`${pagina} redireciona para /aceite com aceite pendente`, async ({ page }) => {
      await mockMe(page, { consentimentoPendente: true });
      await page.goto(url(pagina));
      await expect(page).toHaveURL(new RegExp(`/aceite\\?destino=%2F${pagina}`));
    });
  }

  test('/hoje redireciona para /aceite com aceite pendente', async ({ page, context }) => {
    await context.addCookies([
      { name: 'fisga_e2e_pendente', value: '1', domain: 'localhost', path: '/' }
    ]);
    await page.goto(url('hoje'));
    await expect(page).toHaveURL(/\/aceite\?destino=%2Fhoje/);
  });
});
