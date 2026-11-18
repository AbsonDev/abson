import { test, expect, type Page } from '@playwright/test';

/**
 * Suíte da tela `/conta` (Task 12).
 *
 * A API real é substituída por `page.route`: a Task 13 é quem exercita o
 * backend de ponta a ponta. Aqui validamos a UI, os estados e o contrato que o
 * navegador envia.
 */

const API = 'http://localhost:5184';

type SessaoFake = {
  id: string;
  userAgent: string | null;
  criadoEm: string;
  ultimoUsoEm: string;
  expiraEm: string;
  atual: boolean;
};

type ContaFake = {
  temSenha?: boolean;
  googleConectado?: boolean;
  googleDisponivel?: boolean;
  sessoes?: SessaoFake[];
};

function agoraMenos(minutos: number) {
  return new Date(Date.now() - minutos * 60_000).toISOString();
}

function sessoesPadrao(): SessaoFake[] {
  return [
    {
      id: 'sessao-atual',
      userAgent:
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36',
      criadoEm: agoraMenos(30),
      ultimoUsoEm: agoraMenos(1),
      expiraEm: agoraMenos(-60 * 24 * 30),
      atual: true
    }
  ];
}

async function mockConta(page: Page, opcoes: ContaFake = {}) {
  const estado = {
    temSenha: opcoes.temSenha ?? false,
    googleConectado: opcoes.googleConectado ?? false,
    googleDisponivel: opcoes.googleDisponivel ?? true,
    sessoes: opcoes.sessoes ?? sessoesPadrao()
  };

  await page.route(`${API}/api/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        autenticado: true,
        usuario: {
          id: 'usuario-1',
          email: 'pescador@fisga.app',
          nome: 'Marina Sá',
          nomeExibicao: 'Marina Sá',
          aceitaNovidades: false,
          criadoEm: agoraMenos(60 * 24 * 90)
        },
        consentimentoPendente: false,
        googleConectado: estado.googleConectado,
        temSenha: estado.temSenha
      })
    })
  );

  await page.route(`${API}/api/conta/sessoes`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(estado.sessoes)
    })
  );

  await page.route(`${API}/api/auth/google/disponivel`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ disponivel: estado.googleDisponivel })
    })
  );
}

async function abrirConta(page: Page, opcoes: ContaFake = {}) {
  await mockConta(page, opcoes);
  await page.goto('/conta');
  await expect(page.locator('#conta-content')).toBeVisible();
}

test.describe('Conta — senha', () => {
  test('conta sem senha cria a primeira senha sem pedir senha atual', async ({ page }) => {
    let corpo: Record<string, unknown> | null = null;

    await abrirConta(page, { temSenha: false });
    await page.route(`${API}/api/conta/senha`, async (route) => {
      corpo = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 204, body: '' });
    });

    await expect(page.locator('#conta-senha-acao')).toHaveText('Criar senha');
    await page.locator('#conta-senha-acao').click();

    await expect(page.locator('#dialog-senha')).toBeVisible();
    await expect(page.locator('#senha-titulo')).toHaveText('Criar uma senha');
    await expect(page.locator('#campo-senha-atual')).toBeHidden();

    await page.locator('#nova-senha').fill('minha-nova-senha');
    await page.locator('#confirmar-senha').fill('minha-nova-senha');
    await page.locator('#senha-salvar').click();

    await expect(page.locator('#dialog-senha')).toBeHidden();
    await expect(page.locator('#senha-feedback')).toContainText('Senha criada');
    await expect(page.locator('#conta-senha-acao')).toHaveText('Alterar senha');
    expect(corpo).toEqual({ senhaAtual: null, novaSenha: 'minha-nova-senha' });
  });

  test('conta com senha altera a senha exigindo a senha atual', async ({ page }) => {
    let corpo: Record<string, unknown> | null = null;

    await abrirConta(page, { temSenha: true });
    await page.route(`${API}/api/conta/senha`, async (route) => {
      corpo = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 204, body: '' });
    });

    await expect(page.locator('#conta-senha-acao')).toHaveText('Alterar senha');
    await page.locator('#conta-senha-acao').click();

    await expect(page.locator('#senha-titulo')).toHaveText('Alterar senha');
    await expect(page.locator('#campo-senha-atual')).toBeVisible();

    await page.locator('#senha-atual').fill('senha-atual-123');
    await page.locator('#nova-senha').fill('minha-nova-senha');
    await page.locator('#confirmar-senha').fill('minha-nova-senha');
    await page.locator('#senha-salvar').click();

    await expect(page.locator('#dialog-senha')).toBeHidden();
    await expect(page.locator('#senha-feedback')).toContainText('Senha alterada');
    expect(corpo).toEqual({ senhaAtual: 'senha-atual-123', novaSenha: 'minha-nova-senha' });
  });

  test('senha atual incorreta vira erro no campo, sem fechar o diálogo', async ({ page }) => {
    await abrirConta(page, { temSenha: true });
    await page.route(`${API}/api/conta/senha`, (route) =>
      route.fulfill({
        status: 400,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          codigo: 'senha_atual_incorreta',
          detalhe: 'A senha atual está incorreta.'
        })
      })
    );

    await page.locator('#conta-senha-acao').click();
    await page.locator('#senha-atual').fill('senha-errada-123');
    await page.locator('#nova-senha').fill('minha-nova-senha');
    await page.locator('#confirmar-senha').fill('minha-nova-senha');
    await page.locator('#senha-salvar').click();

    await expect(page.locator('#dialog-senha')).toBeVisible();
    await expect(page.locator('#senha-atual-error')).toBeVisible();
    await expect(page.locator('#senha-atual-error')).toContainText('senha atual está incorreta');
    await expect(page.locator('#senha-atual')).toHaveAttribute('aria-invalid', 'true');
  });

  test('validação de tamanho acontece antes de chamar a API', async ({ page }) => {
    let chamadas = 0;

    await abrirConta(page, { temSenha: false });
    await page.route(`${API}/api/conta/senha`, (route) => {
      chamadas += 1;
      return route.fulfill({ status: 204, body: '' });
    });

    await page.locator('#conta-senha-acao').click();
    await page.locator('#nova-senha').fill('123456789');
    await page.locator('#confirmar-senha').fill('123456789');
    await page.locator('#senha-salvar').click();

    await expect(page.locator('#nova-senha-error')).toBeVisible();
    await expect(page.locator('#nova-senha-error')).toContainText('pelo menos 10');
    expect(chamadas).toBe(0);
  });

  test('confirmação diferente bloqueia o envio', async ({ page }) => {
    let chamadas = 0;

    await abrirConta(page, { temSenha: false });
    await page.route(`${API}/api/conta/senha`, (route) => {
      chamadas += 1;
      return route.fulfill({ status: 204, body: '' });
    });

    await page.locator('#conta-senha-acao').click();
    await page.locator('#nova-senha').fill('minha-nova-senha');
    await page.locator('#confirmar-senha').fill('outra-senha-123');
    await page.locator('#senha-salvar').click();

    await expect(page.locator('#confirmar-senha-error')).toBeVisible();
    await expect(page.locator('#confirmar-senha-error')).toContainText('não são iguais');
    expect(chamadas).toBe(0);
  });
});

test.describe('Conta — Google', () => {
  test('mostra status e navega para conectar quando disponível', async ({ page }) => {
    await abrirConta(page, { googleConectado: false, googleDisponivel: true });
    await page.route(`${API}/api/auth/google/conectar`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<!doctype html><h1>Google</h1>'
      })
    );

    await expect(page.locator('#google-status')).toHaveText('Não conectado');
    const botao = page.getByRole('button', { name: 'Conectar Google' });
    await expect(botao).toBeVisible();

    await botao.click();
    await expect(page).toHaveURL(`${API}/api/auth/google/conectar`);
  });

  test('conta conectada mostra o estado sem oferecer conectar', async ({ page }) => {
    await abrirConta(page, { googleConectado: true });

    await expect(page.locator('#google-status')).toHaveText('Conectado');
    await expect(page.getByRole('button', { name: 'Conectar Google' })).toHaveCount(0);
  });

  test('retorno com sucesso mostra aviso acessível', async ({ page }) => {
    await mockConta(page, { googleConectado: true });
    await page.goto('/conta?estado=google_conectado');
    await expect(page.locator('#conta-content')).toBeVisible();

    await expect(page.locator('#conta-aviso')).toBeVisible();
    await expect(page.locator('#conta-aviso')).toContainText('Google conectado');
    await expect(page.locator('#conta-aviso [data-aviso-icon="ok"]')).toBeVisible();
  });

  test('e-mail divergente explica o erro sem ícone de sucesso', async ({ page }) => {
    await mockConta(page, { googleConectado: false });
    await page.goto('/conta?estado=google_email_divergente');
    await expect(page.locator('#conta-content')).toBeVisible();

    await expect(page.locator('#conta-aviso')).toBeVisible();
    await expect(page.locator('#conta-aviso')).toContainText('mesmo e-mail');
    await expect(page.locator('#conta-aviso [data-aviso-icon="erro"]')).toBeVisible();
    await expect(page.locator('#conta-aviso [data-aviso-icon="ok"]')).toBeHidden();
  });
});

test.describe('Conta — dispositivos', () => {
  test('lista as sessões com dispositivo, uso e marca da sessão atual', async ({ page }) => {
    await abrirConta(page, {
      sessoes: [
        ...sessoesPadrao(),
        {
          id: 'sessao-iphone',
          userAgent:
            'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
          criadoEm: agoraMenos(60 * 24 * 3),
          ultimoUsoEm: agoraMenos(60 * 24 * 2),
          expiraEm: agoraMenos(-60 * 24 * 30),
          atual: false
        }
      ]
    });

    const sessoes = page.locator('#sessoes-lista .session');
    await expect(sessoes).toHaveCount(2);
    await expect(sessoes.first()).toContainText('Chrome no macOS');
    await expect(sessoes.first()).toContainText('Sessão atual');
    await expect(sessoes.nth(1)).toContainText('Safari no iOS');
  });

  test('sair desta sessão encerra e volta para entrar', async ({ page }) => {
    let chamadas = 0;

    await abrirConta(page);
    await page.route(`${API}/api/auth/sair`, (route) => {
      chamadas += 1;
      return route.fulfill({ status: 204, body: '' });
    });
    await page.route('**/entrar?saiu=1', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<!doctype html><h1>Saiu</h1>'
      })
    );

    await page.locator('#sair-atual').click();
    await expect(page.locator('#dialog-sair')).toBeVisible();
    await page.locator('#sair-confirmar').click();

    await expect(page).toHaveURL(/\/entrar\?saiu=1$/);
    expect(chamadas).toBe(1);
  });

  test('sair de todos encerra todas as sessões após confirmação', async ({ page }) => {
    let chamadas = 0;

    await abrirConta(page);
    await page.route(`${API}/api/auth/sair-de-todos`, (route) => {
      chamadas += 1;
      return route.fulfill({ status: 204, body: '' });
    });
    await page.route('**/entrar?saiu=1', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<!doctype html><h1>Saiu</h1>'
      })
    );

    await page.locator('#sair-todos').click();
    await expect(page.locator('#dialog-sair')).toBeVisible();
    await expect(page.locator('#sair-titulo')).toContainText('todos');
    await page.locator('#sair-confirmar').click();

    await expect(page).toHaveURL(/\/entrar\?saiu=1$/);
    expect(chamadas).toBe(1);
  });
});

test.describe('Conta — zona sensível', () => {
  test('excluir conta exige digitar EXCLUIR e envia a confirmação', async ({ page }) => {
    let metodo = '';
    let corpo: Record<string, unknown> | null = null;

    await abrirConta(page);
    await page.route(`${API}/api/conta`, async (route) => {
      metodo = route.request().method();
      corpo = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 204, body: '' });
    });

    await page.locator('#excluir-conta').click();
    await expect(page.locator('#dialog-excluir')).toBeVisible();
    const confirmar = page.locator('#excluir-confirmar');
    await expect(confirmar).toBeDisabled();

    await page.locator('#excluir-confirmacao').fill('excluir');
    await expect(confirmar).toBeDisabled();

    await page.locator('#excluir-confirmacao').fill('EXCLUIR');
    await expect(confirmar).toBeEnabled();
    await confirmar.click();

    await expect(page.locator('#conta-encerrada')).toBeVisible();
    await expect(page.locator('#conta-encerrada')).toContainText('Conta excluída.');
    expect(metodo).toBe('DELETE');
    expect(corpo).toEqual({ confirmacao: 'EXCLUIR' });
  });
});
