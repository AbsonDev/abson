import { test, expect } from '@playwright/test';

/**
 * A decisão de cache do service worker é um script compartilhado
 * (`/sw-politicas.js`), carregado pelo `sw.js` com `importScripts`. Aqui o
 * mesmo arquivo é injetado na página para testar a função pura sem depender de
 * um service worker ativo.
 */

type Politicas = {
  podeCachear: (resposta: unknown) => boolean;
  rotaPrivada: (caminho: string) => boolean;
  paginaPublica: (caminho: string) => boolean;
};

declare global {
  interface Window {
    FisgaSWPoliticas: Politicas;
  }
}

test('podeCachear segue o header e não o path', async ({ page }) => {
  await page.goto('/offline');
  await page.addScriptTag({ url: '/sw-politicas.js' });

  const resultados = await page.evaluate(() => {
    const p = window.FisgaSWPoliticas;
    const fake = (ok: boolean, headers: Record<string, string>) => {
      const mapa = new Map(Object.entries(headers).map(([c, v]) => [c.toLowerCase(), v]));
      return { ok, headers: { get: (nome: string) => mapa.get(nome.toLowerCase()) ?? null } };
    };

    return {
      noStore: p.podeCachear(fake(true, { 'cache-control': 'private, no-store' })),
      privateMaxAge: p.podeCachear(fake(true, { 'cache-control': 'private, max-age=30' })),
      setCookie: p.podeCachear(fake(true, { 'set-cookie': 'fisga_auth=abc' })),
      publico: p.podeCachear(fake(true, { 'cache-control': 'public, max-age=60' })),
      publicoHtml: p.podeCachear(
        fake(true, {
          'cache-control': 'public, max-age=3600, stale-while-revalidate=86400',
          'content-type': 'text/html; charset=utf-8'
        })
      ),
      semHeaderHtml: p.podeCachear(
        fake(true, { 'content-type': 'text/html; charset=utf-8' })
      ),
      semHeaderImagem: p.podeCachear(fake(true, { 'content-type': 'image/png' })),
      semHeaderSemTipo: p.podeCachear(fake(true, {})),
      maxAgeSemPublicoHtml: p.podeCachear(
        fake(true, { 'cache-control': 'max-age=60', 'content-type': 'text/html' })
      ),
      erro: p.podeCachear(fake(false, { 'cache-control': 'public, max-age=60' })),
      nulo: p.podeCachear(null),
      hoje: p.rotaPrivada('/hoje'),
      hojeFilho: p.rotaPrivada('/hoje/123'),
      condicoes: p.rotaPrivada('/condicoes'),
      condicoesPublica: p.paginaPublica('/condicoes/'),
      raizPublica: p.paginaPublica('/')
    };
  });

  expect(resultados.noStore).toBe(false);
  expect(resultados.privateMaxAge).toBe(false);
  expect(resultados.setCookie).toBe(false);
  expect(resultados.publico).toBe(true);
  expect(resultados.publicoHtml).toBe(true);
  // Default-deny: HTML sem `public` explícito nunca é cacheado. Só assets com
  // tipo conhecidamente não-HTML passam sem header.
  expect(resultados.semHeaderHtml).toBe(false);
  expect(resultados.semHeaderImagem).toBe(true);
  expect(resultados.semHeaderSemTipo).toBe(false);
  expect(resultados.maxAgeSemPublicoHtml).toBe(false);
  expect(resultados.erro).toBe(false);
  expect(resultados.nulo).toBe(false);
  expect(resultados.hoje).toBe(true);
  expect(resultados.hojeFilho).toBe(true);
  expect(resultados.condicoes).toBe(false);
  expect(resultados.condicoesPublica).toBe(true);
  expect(resultados.raizPublica).toBe(true);
});
