import { test, expect } from '@playwright/test';

test('conta é privada', async ({ request }) => {
  const r = await request.get('/conta');
  expect(r.headers()['cache-control']).toBe('private, no-store');
});

test('condicoes anonima é publica', async ({ request }) => {
  const r = await request.get('/condicoes');
  expect(r.headers()['cache-control']).toContain('public');
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
