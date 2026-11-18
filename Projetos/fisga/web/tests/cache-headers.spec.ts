import { test, expect } from '@playwright/test';

const CONTEUDO_PUBLICO = 'public, max-age=3600, stale-while-revalidate=86400';

test('conta é privada', async ({ request }) => {
  const r = await request.get('/conta');
  expect(r.headers()['cache-control']).toBe('private, no-store');
});

test('condicoes anonima é publica', async ({ request }) => {
  const r = await request.get('/condicoes');
  expect(r.headers()['cache-control']).toContain('public');
});

test('condicoes anonima varia por cookie', async ({ request }) => {
  const r = await request.get('/condicoes');
  expect(r.headers()['vary']?.toLowerCase()).toContain('cookie');
});

test('raiz anonima varia por cookie', async ({ request }) => {
  const r = await request.get('/');
  expect(r.headers()['cache-control']).toContain('public');
  expect(r.headers()['vary']?.toLowerCase()).toContain('cookie');
});

test('condicoes com cookie é privada', async ({ request }) => {
  const r = await request.get('/condicoes', {
    headers: { cookie: 'fisga_auth=teste' }
  });
  expect(r.headers()['cache-control']).toBe('private, no-store');
});

test('hoje redireciona com header privado', async ({ request }) => {
  const r = await request.get('/hoje', { maxRedirects: 0 });
  expect(r.status()).toBe(302);
  expect(r.headers()['cache-control']).toBe('private, no-store');
});

test('404 responde no-store', async ({ request }) => {
  const r = await request.get('/caminho-que-nao-existe');
  expect(r.status()).toBe(404);
  expect(r.headers()['cache-control']).toBe('private, no-store');
});

test('páginas públicas de acesso e offline alimentam o precache', async ({ request }) => {
  for (const caminho of [
    '/entrar',
    '/entrar/confirmar',
    '/criar-conta',
    '/esqueci-senha',
    '/redefinir-senha',
    '/confirmar-email',
    '/offline'
  ]) {
    const r = await request.get(caminho);
    expect(r.ok(), `${caminho} deveria responder 2xx`).toBe(true);
    expect(r.headers()['cache-control'], caminho).toBe(CONTEUDO_PUBLICO);
  }
});
