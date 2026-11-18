import { test, expect } from '@playwright/test';
import { limparTexto, limparUrl } from '../src/lib/sentry-scrub';

test('limparUrl filtra termo de busca e preserva paginação', () => {
  const url = limparUrl(
    'https://fisga.abson.tech/condicoes?q=S%C3%A3o%20Jo%C3%A3o&busca=Paraty&termo=Angra&page=2'
  );

  expect(url).toContain('q=[Filtered]');
  expect(url).toContain('busca=[Filtered]');
  expect(url).toContain('termo=[Filtered]');
  expect(url).toContain('page=2');
  expect(url).not.toContain('S%C3%A3o');
  expect(url).not.toContain('Paraty');
  expect(url).not.toContain('Angra');
});

test('limparUrl filtra chave desconhecida por padrão', () => {
  const url = limparUrl('https://fisga.abson.tech/mapa?futuro=segredo&lang=pt-BR');

  expect(url).toContain('futuro=[Filtered]');
  expect(url).toContain('lang=pt-BR');
  expect(url).not.toContain('segredo');
});

test('limparTexto filtra q em mensagem com URL codificada', () => {
  const texto = limparTexto('Falha em /api/locais/buscar?q=S%C3%A3o%20Jo%C3%A3o');

  expect(texto).toContain('q=[Filtered]');
  expect(texto).not.toContain('S%C3%A3o');
});
