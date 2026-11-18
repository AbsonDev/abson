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
      semHeader: p.podeCachear(fake(true, {})),
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
  // Sem header, a política atual permite cachear respostas 2xx same-origin.
  expect(resultados.semHeader).toBe(true);
  expect(resultados.erro).toBe(false);
  expect(resultados.nulo).toBe(false);
  expect(resultados.hoje).toBe(true);
  expect(resultados.hojeFilho).toBe(true);
  expect(resultados.condicoes).toBe(false);
  expect(resultados.condicoesPublica).toBe(true);
  expect(resultados.raizPublica).toBe(true);
});
