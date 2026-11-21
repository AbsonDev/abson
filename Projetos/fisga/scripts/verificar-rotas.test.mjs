import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync
} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  extrairDoContrato,
  extrairDoReadme,
  extrairDoInventario,
  divergencias
} from './verificar-rotas.mjs';

const SCRIPT = fileURLToPath(new URL('./verificar-rotas.mjs', import.meta.url));
const METODOS = ['get', 'post', 'put', 'patch', 'delete'];

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
  // O mesmo documento com CRLF (Windows/editor antigo) não pode zerar a seção.
  assert.deepEqual(
    [...extrairDoReadme(markdown.replaceAll('\n', '\r\n'))].sort(),
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

test('extratores leem as mesmas operacoes nos tres documentos reais', () => {
  const ler = (caminho) => readFileSync(new URL(caminho, import.meta.url), 'utf8');
  const contrato = extrairDoContrato(JSON.parse(ler('../docs/api/openapi.json')));
  const readme = extrairDoReadme(ler('../README.md'));
  const inventario = extrairDoInventario(ler('../docs/api/inventario.md'));

  assert.ok(contrato.size > 0, 'o contrato não pode ficar vazio');
  assert.equal(readme.size, contrato.size);
  assert.equal(inventario.size, contrato.size);
  assert.deepEqual(divergencias(contrato, readme, inventario), {
    faltamNoReadme: [],
    faltamNoInventario: [],
    sobramNoReadme: [],
    sobramNoInventario: []
  });
});

test('contrato so usa os cinco metodos comparados pela guarda', () => {
  const contrato = JSON.parse(
    readFileSync(new URL('../docs/api/openapi.json', import.meta.url), 'utf8'));
  // Chaves legais de um Path Item que não são operação (OpenAPI 3.1).
  const naoOperacao = new Set(['$ref', 'summary', 'description', 'servers', 'parameters']);
  const metodos = new Set(
    Object.values(contrato.paths)
      .flatMap((caminho) => Object.keys(caminho))
      .filter((chave) => !naoOperacao.has(chave)));

  for (const metodo of metodos) {
    assert.ok(
      METODOS.includes(metodo),
      `Método fora da guarda de rotas: ${metodo}. Acrescente em METODOS ou remova.`);
  }
});

test('CLI roda de verdade e confirma os tres documentos', () => {
  const saida = execFileSync(process.execPath, [SCRIPT], { encoding: 'utf8' });
  // A contagem exata é invariante do contrato, coberta por ContratoTests; aqui
  // o alvo é o CLI rodar e não divergir. Um \d+ evita que rota nova quebre este
  // teste com mensagem enganosa.
  assert.match(
    saida,
    /Rotas em sincronia: \d+ operações no contrato, no README e no inventário\./);
});

function prepararProjeto(transformarReadme = (markdown) => markdown) {
  const tmp = mkdtempSync(path.join(os.tmpdir(), 'rotas-'));
  mkdirSync(path.join(tmp, 'scripts'));
  mkdirSync(path.join(tmp, 'docs', 'api'), { recursive: true });
  copyFileSync(SCRIPT, path.join(tmp, 'scripts', 'verificar-rotas.mjs'));
  copyFileSync(
    new URL('../docs/api/openapi.json', import.meta.url),
    path.join(tmp, 'docs', 'api', 'openapi.json'));
  copyFileSync(
    new URL('../docs/api/inventario.md', import.meta.url),
    path.join(tmp, 'docs', 'api', 'inventario.md'));
  writeFileSync(
    path.join(tmp, 'README.md'),
    transformarReadme(readFileSync(new URL('../README.md', import.meta.url), 'utf8')));
  return tmp;
}

function rodarCli(tmp) {
  return execFileSync(
    process.execPath,
    [path.join(tmp, 'scripts', 'verificar-rotas.mjs')],
    { encoding: 'utf8', stdio: 'pipe' });
}

test('CLI sai 1 quando o README nao tem a secao API', () => {
  const tmp = prepararProjeto((markdown) => markdown.replace(/^## API$/m, '## Interface'));
  try {
    assert.throws(
      () => rodarCli(tmp),
      (error) => {
        assert.equal(error.status, 1);
        assert.match(error.stderr, /Documento sem nenhuma rota lida: o README\.md/);
        return true;
      });
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
});

test('CLI sai 1 e aponta a rota que falta quando o README diverge', () => {
  const tmp = prepararProjeto((markdown) => markdown
    .split(/\r?\n/)
    .filter((linha) => !linha.includes('`/api/especies/sugerir`'))
    .join('\n'));
  try {
    assert.throws(
      () => rodarCli(tmp),
      (error) => {
        assert.equal(error.status, 1);
        assert.match(error.stderr, /Faltam no README\.md:/);
        assert.match(error.stderr, /POST \/api\/especies\/sugerir/);
        return true;
      });
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
});

test('CLI sai 1 e aponta a rota que sobra quando o README inventa', () => {
  const tmp = prepararProjeto((markdown) => markdown.replace(
    /^## API$/m,
    '## API\n\n| `GET` | `/api/rota-fantasma` | Público | Não existe |'));
  try {
    assert.throws(
      () => rodarCli(tmp),
      (error) => {
        assert.equal(error.status, 1);
        assert.match(error.stderr, /Sobram no README\.md \(não estão no contrato\):/);
        assert.match(error.stderr, /GET \/api\/rota-fantasma/);
        return true;
      });
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
});
