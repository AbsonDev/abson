import { test, expect, type Page } from '@playwright/test';

/**
 * Suíte mockada do seletor de nível de detalhe do local em `/registrar`.
 *
 * No describe de registro, a API é interceptada no navegador com `page.route`:
 * validamos a interface e o corpo que o formulário envia, sem depender de
 * backend. O describe de link público e troféu usa o SSR servido por
 * `tests/mock-api.mjs`, que fornece as rotas `/p/<token>` e `/trofeu/...`.
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

  test('limpa o campo ao trocar para um nível que não usa rio ou trecho', async ({ page }) => {
    await abrirRegistrar(page);

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar rio ou lagoa' }).click();
    const campo = page.locator('#corpo-ou-trecho');
    await campo.fill('Rio Sergipe, trecho centro');

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar cidade/UF' }).click();
    await expect(page.locator('#campo-corpo-ou-trecho')).toBeHidden();
    await expect(campo).toHaveValue('');

    await page.locator('label.priv-opt').filter({ hasText: 'Mostrar trecho' }).click();
    await expect(page.locator('#campo-corpo-ou-trecho')).toBeVisible();
    await expect(campo).toHaveValue('');

    await campo.fill('Rio Sergipe, trecho centro');
    await page.locator('label.priv-opt').filter({ hasText: 'Ocultar local' }).click();
    await expect(campo).toHaveValue('');
  });

  test('restaura rascunho com rio ou trecho e o nível escolhido', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem(
        'fisga:rascunho:v1',
        JSON.stringify({
          entrada: {
            privacidadeDoLocal: 'CorpoAgua',
            corpoOuTrecho: 'Rio Sergipe, trecho centro',
            capturas: []
          },
          atualizadoEm: new Date().toISOString()
        })
      );
    });

    await abrirRegistrar(page);

    await expect(page.locator('#priv-agua')).toBeChecked();
    await expect(page.locator('#campo-corpo-ou-trecho')).toBeVisible();
    await expect(page.locator('#corpo-ou-trecho')).toHaveValue('Rio Sergipe, trecho centro');
    await expect(page.locator('#privacidade-ajuda')).toHaveText(
      'Aparece só o nome do rio ou da lagoa, sem o ponto exato.'
    );
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
    // O POST é atribuído dentro do callback de `page.route`; o cast desfaz o
    // estreitamento estático de `corpoPost` para `null` neste ponto.
    const corpo = corpoPost as Record<string, unknown> | null;
    expect(corpo?.privacidadeDoLocal).toBe('CorpoAgua');
    expect(corpo?.corpoOuTrecho).toBe('Rio Sergipe, trecho centro');
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

test.describe('Link público e troféu — badge por nível', () => {
  test('mostra o badge de Cidade e o local uma vez no cabeçalho', async ({ page }) => {
    await page.goto('/p/niveis-cidade');

    const cabecalho = page.locator('.calculo-topo');
    await expect(cabecalho.getByText('Cidade aproximada', { exact: true })).toBeVisible();
    await expect(cabecalho).toContainText('Campinas · SP');

    const ocorrencias = await cabecalho.evaluate(
      (elemento) => (elemento.textContent?.match(/Campinas · SP/g) ?? []).length
    );
    expect(ocorrencias).toBe(1);
  });

  test('troféu mostra o local uma vez no card e no canvas', async ({ page }) => {
    await page.goto('/trofeu/niveis-cidade/captura-cidade');

    const rodape = page.locator('.trofeu-rodape');
    await expect(rodape).toContainText('Campinas · SP');

    const ocorrencias = await rodape.evaluate(
      (elemento) => (elemento.textContent?.match(/Campinas · SP/g) ?? []).length
    );
    expect(ocorrencias).toBe(1);

    const dados = JSON.parse((await page.locator('#trofeu-dados').textContent()) ?? '{}');
    expect(dados.local).toBe('Campinas · SP');
  });
});
