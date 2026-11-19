import { test, expect, type Page } from '@playwright/test';

/**
 * Suíte mockada do seletor de nível de detalhe do local em `/registrar`.
 *
 * A API é interceptada no navegador com `page.route`; o objetivo aqui é validar
 * a interface e o corpo que o formulário envia, sem depender de backend.
 */

async function abrirRegistrar(page: Page, caminho = '/registrar') {
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

  await page.goto(caminho);
  await expect
    .poll(() => page.locator('#pescaria-form').evaluate((elemento) => (elemento as HTMLElement).inert))
    .toBe(false);
}

test.describe('Registro — nível de detalhe do local', () => {
  test('Cidade vem marcada por padrão', async ({ page }) => {
    await abrirRegistrar(page);

    await expect(page.getByRole('radio', { name: /Mostrar cidade\/UF/ })).toBeChecked();
  });

  test('a ajuda compartilhada mostra só o texto do nível ativo', async ({ page }) => {
    await abrirRegistrar(page);

    const ajuda = page.locator('#privacidade-ajuda');
    await expect(ajuda).toContainText('Aparecem cidade e UF, sem praia, rio ou coordenadas.');

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar rio ou lagoa' }).click();
    await expect(ajuda).toHaveText('Aparece só o nome do rio ou da lagoa, sem o ponto exato.');

    await page.locator('label.priv-opt').filter({ hasText: 'Ocultar local' }).click();
    await expect(ajuda).toContainText('O link não mostra nenhum lugar.');
  });

  test('o campo de rio ou trecho aparece só para Trecho e CorpoAgua', async ({ page }) => {
    await abrirRegistrar(page);

    const campo = page.locator('#campo-corpo-ou-trecho');
    await expect(campo).toBeHidden();

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar local exato' }).click();
    await expect(campo).toBeHidden();

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar trecho' }).click();
    await expect(campo).toBeVisible();
    await expect(page.locator('#corpo-ou-trecho')).toHaveAttribute('maxlength', '80');
    await expect(page.locator('#corpo-ou-trecho')).toHaveAttribute(
      'placeholder',
      'Ex.: Rio Sergipe, trecho centro'
    );

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar rio ou lagoa' }).click();
    await expect(campo).toBeVisible();

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar cidade/UF' }).click();
    await expect(campo).toBeHidden();
  });

  test('envia o nível escolhido e o rio/trecho no corpo do POST', async ({ page }) => {
    await abrirRegistrar(page);

    let corpoPost: Record<string, unknown> | null = null;
    await page.route('**/api/pescarias', async (route) => {
      corpoPost = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ shareToken: 'token-e2e' })
      });
    });
    await page.route('**/p/**', (route) =>
      route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><h1>Salvo</h1>' })
    );

    await page.locator('#local').fill('Poita do Zé');
    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar rio ou lagoa' }).click();
    await page.locator('#corpo-ou-trecho').fill('Rio Sergipe, trecho centro');

    await page.locator('[data-etapa="1"] [data-avancar]').click();
    await page.locator('[data-etapa="2"] [data-avancar]').click();
    await page.locator('[data-etapa="3"] [data-avancar]').click();
    await page.locator('#btn-salvar').click();

    await expect.poll(() => corpoPost).not.toBeNull();
    expect(corpoPost?.privacidadeDoLocal).toBe('CorpoAgua');
    expect(corpoPost?.corpoOuTrecho).toBe('Rio Sergipe, trecho centro');
  });

  test('o preenchimento vindo de /hoje mantém Cidade como padrão', async ({ page }) => {
    await page.route('**/api/condicoes*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          local: { nome: 'Poita do Zé', cidade: 'Aracaju', estado: 'SE', ambiente: 'Rio' },
          atualizadoEm: new Date().toISOString(),
          temMare: false,
          solunar: { fase: 'Cheia', faseNome: 'Cheia' },
          tempo: { ventoKmh: 10, chuvaProbabilidade: 0 }
        })
      })
    );

    await abrirRegistrar(
      page,
      '/registrar?lat=-10.9472&lon=-37.0731&nome=Poita%20do%20Z%C3%A9&cidade=Aracaju&estado=SE&ambiente=Rio'
    );

    await expect(page.locator('#condicoes-aplicadas')).toBeVisible();
    await expect(page.locator('#priv-cidade')).toBeChecked();
    await expect(page.locator('#priv-exato')).not.toBeChecked();
  });
});
