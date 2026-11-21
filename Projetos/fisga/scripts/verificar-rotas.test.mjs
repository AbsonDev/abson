import test from 'node:test';
import assert from 'node:assert/strict';
import {
  extrairDoContrato,
  extrairDoReadme,
  extrairDoInventario,
  divergencias
} from './verificar-rotas.mjs';

test('contrato publica metodo e rota de cada operacao', () => {
  const documento = {
    paths: {
      '/health': { get: {} },
      '/api/x/{id}': { get: {}, delete: {} },
      '/api/x': { post: {} }
    }
  };
  assert.deepEqual(
    [...extrairDoContrato(documento)].sort(),
    ['DELETE /api/x/{id}', 'GET /api/x/{id}', 'GET /health', 'POST /api/x']);
});

test('README le a tabela do resumo, apara query e separa celula combinada', () => {
  const markdown = [
    '# Projeto',
    '## API',
    '',
    '| Método | Rota | Acesso | Descrição |',
    '| --- | --- | --- | --- |',
    '| `GET` | `/health`, `/ready` | Público | Saúde |',
    '| `GET` | `/api/auth/confirmar?token=` | Público | Confirma |',
    '| `POST` | `/api/pescarias` | Sessão | Cria |',
    '',
    '## Privacidade',
    '| `GET` | `/api/fora-da-secao` | X | Y |'
  ].join('\n');
  assert.deepEqual(
    [...extrairDoReadme(markdown)].sort(),
    ['GET /api/auth/confirmar', 'GET /health', 'GET /ready', 'POST /api/pescarias']);
});

test('inventario le as linhas de operacao da secao Dominios', () => {
  const markdown = [
    '## Regras transversais',
    '| `GET /api/fora` | `public` |',
    '## Domínios',
    '### Sistema',
    '| `GET /health` | P | — | no-store | `200` | — |',
    '| `POST /api/newsletter/enviar` | A | — | no-store | `200` | `404` |',
    '## Lacunas de contrato'
  ].join('\n');
  assert.deepEqual(
    [...extrairDoInventario(markdown)].sort(),
    ['GET /health', 'POST /api/newsletter/enviar']);
});

test('divergencia lista o que falta em cada documento', () => {
  const contrato = new Set(['GET /health', 'GET /api/novo']);
  const readme = new Set(['GET /health', 'GET /api/antigo']);
  const inventario = new Set(['GET /health', 'GET /api/antigo']);

  assert.deepEqual(divergencias(contrato, readme, inventario), {
    faltamNoReadme: ['GET /api/novo'],
    faltamNoInventario: ['GET /api/novo'],
    sobramNoReadme: ['GET /api/antigo'],
    sobramNoInventario: ['GET /api/antigo']
  });
});
