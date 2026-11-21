// Testes do gerador de referência: o contrato é a fonte do que é mecânico
// (método, rota, acesso, limites, cache, erros) e o override cobre o resto.
// Os casos de contrato real leem docs/api/openapi.json da árvore.
import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import {
  DOMINIOS,
  ErroDeGeracao,
  auditar,
  derivarConsentimento,
  derivarErros,
  gerarModelo,
  lerContrato,
  lerOverrides,
  listarOperacoes,
  montarCurl,
  slugDoDominio,
  validarOverride
} from './gerar-referencia.mjs';

const TAGS = ['Sistema', 'Catálogo', 'Autenticação', 'Conta', 'Pescarias', 'Diário',
  'Condições', 'Locais', 'Pontos', 'Trilhas', 'Mídia', 'Espécies', 'Newsletter'];

const CONTRATO = {
  info: { title: 'API do Fisga', description: 'Descrição.', version: '1.0.0' },
  servers: [{ url: 'https://api.fisga.exemplo.tech', description: 'Produção' }],
  tags: TAGS.map((name) => ({ name, description: `Descrição de ${name}.` })),
  paths: {
    '/health': {
      get: {
        operationId: 'obterSaude', summary: 'Saúde', description: 'Diz se está no ar.',
        tags: ['Sistema'], security: [], 'x-cache': 'private, no-store',
        responses: { '200': { description: 'OK' } }
      }
    },
    '/api/conta': {
      delete: {
        operationId: 'excluirConta', summary: 'Exclui a conta', description: 'Exclui.',
        tags: ['Conta'], security: [{ cookieAuth: [] }],
        'x-rate-limit': { policy: 'escrita', requests: 40, windowSeconds: 60 },
        'x-cache': 'private, no-store',
        requestBody: {
          required: true,
          content: { 'application/json': { schema: { $ref: '#/components/schemas/ExcluirContaRequest' } } }
        },
        responses: {
          '204': { description: 'Sem corpo' },
          '400': { description: 'Corpo inválido.', content: { 'application/json': {} } },
          '401': { description: 'Sessão ausente (`nao_autenticado`).' },
          '403': { description: 'Sem permissão (`acesso_negado`) ou consentimento pendente (`consentimento_pendente`).' },
          '429': { description: 'Muitas requisições. Corpo vazio; o header `Retry-After` informa os segundos.' }
        }
      }
    },
    '/api/locais/{id}': {
      delete: {
        operationId: 'excluirLocal', summary: 'Exclui o local', description: 'Exclui.',
        tags: ['Locais'], security: [{ cookieAuth: [] }],
        parameters: [
          { name: 'id', in: 'path', required: true, schema: { type: 'string', format: 'uuid' } },
          { name: 'motivo', in: 'query', schema: { type: 'string' } }
        ],
        responses: {
          '204': { description: 'Sem corpo' },
          '404': { description: 'Recurso não encontrado. Corpo vazio.' }
        }
      }
    },
    '/api/condicoes': {
      get: {
        operationId: 'obterCondicoes', summary: 'Condições', description: 'Clima e maré.',
        tags: ['Condições'], security: [], 'x-cache': 'public, max-age=300',
        parameters: [
          { name: 'lat', in: 'query', schema: { type: ['number', 'string'], format: 'double' } },
          { name: 'lon', in: 'query', schema: { type: ['number', 'string'], format: 'double' } }
        ],
        responses: {
          '200': { description: 'OK', content: { 'application/json': { schema: { $ref: '#/components/schemas/Condicoes' } } } },
          '400': { description: 'Requisição inválida.', content: { 'application/json': {} } }
        }
      }
    },
    '/api/newsletter/enviar': {
      post: {
        operationId: 'enviarNewsletter', summary: 'Envia a newsletter', description: 'Dispara o envio.',
        tags: ['Newsletter'], security: [{ adminToken: [] }], 'x-cache': 'private, no-store',
        responses: {
          '200': { description: 'OK' },
          '404': { description: 'Recurso não encontrado. Corpo vazio.' },
          '500': { description: 'Erro inesperado (`codigo`: `erro_inesperado`).' }
        }
      }
    },
    '/api/pescarias': {
      post: {
        operationId: 'criarPescaria', summary: 'Cria a pescaria', description: 'Cria.',
        tags: ['Pescarias'], security: [{ cookieAuth: [] }], 'x-cache': 'private, no-store',
        requestBody: {
          required: true,
          content: { 'application/json': { schema: { $ref: '#/components/schemas/EntradaDePescaria' } } }
        },
        responses: {
          '201': { description: 'Criado', headers: { Location: { schema: { type: 'string' } } } },
          '400': { description: 'Corpo inválido.', content: { 'application/json': {} } }
        }
      }
    },
    '/api/conta/sessoes': {
      get: {
        operationId: 'listarSessoes', summary: 'Lista as sessões', description: 'Lista.',
        tags: ['Conta'], security: [{ cookieAuth: [] }], 'x-cache': 'private, no-store',
        responses: { '200': { description: 'OK' } }
      }
    },
    '/api/locais/buscar': {
      get: {
        operationId: 'buscarLocais', summary: 'Busca locais', description: 'Busca.',
        tags: ['Locais'], security: [], 'x-cache': 'public, max-age=3600',
        parameters: [{ name: 'q', in: 'query', schema: { type: 'string' } }],
        responses: { '200': { description: 'OK' } }
      }
    },
    '/api/midia': {
      post: {
        operationId: 'enviarMidia', summary: 'Envia a foto', description: 'Envia.',
        tags: ['Mídia'], security: [{ cookieAuth: [] }], 'x-cache': 'private, no-store',
        requestBody: {
          required: true,
          content: { 'multipart/form-data': { schema: { $ref: '#/components/schemas/FormularioDeFoto' } } }
        },
        responses: {
          '200': { description: 'OK' },
          '400': { description: 'Arquivo inválido.', content: { 'application/json': {} } }
        }
      }
    }
  },
  components: {
    schemas: {
      Condicoes: { type: 'object', required: ['agora'], properties: { agora: { type: 'string', format: 'date-time' }, tempo: { type: ['null', 'string'] } } },
      ExcluirContaRequest: { type: 'object', required: ['confirmacao'], properties: { confirmacao: { type: ['null', 'string'] } } },
      EntradaDePescaria: {
        type: 'object',
        properties: {
          local: { type: ['null', 'string'] },
          ambiente: { $ref: '#/components/schemas/AmbienteDePesca' },
          data: { type: 'string', format: 'date' },
          periodo: { $ref: '#/components/schemas/PeriodoDoDia' }
        }
      },
      AmbienteDePesca: { type: 'string', enum: ['Rio', 'Mar', 'Lagoa', 'Represa'] },
      PeriodoDoDia: { type: 'string', enum: ['Madrugada', 'Manha', 'Tarde', 'Noite'] },
      FormularioDeFoto: { type: 'object', required: ['arquivo'], properties: { arquivo: { type: 'string', format: 'binary' } } }
    },
    securitySchemes: {
      cookieAuth: { type: 'apiKey', in: 'cookie', name: 'fisga_auth' },
      adminToken: { type: 'apiKey', in: 'header', name: 'X-Admin-Token' }
    }
  }
};

// Um override por operação da fixture. Aqui o conteúdo ainda não é validado;
// do Task 3 em diante ele passa por `validarOverride`.
const OVERRIDES = new Map([
  ['obterSaude', { arquivo: 'obterSaude.md', frontmatter: { operacao: 'obterSaude', proposta: 'Diz se está no ar.', acesso: 'publico', verificar: 'Veja `status`.' }, corpo: '' }],
  ['excluirConta', { arquivo: 'excluirConta.md', frontmatter: { operacao: 'excluirConta', proposta: 'Exclui a conta.', acesso: 'sessao', verificar: 'Veja o `204`.', corpo: '{"confirmacao":"EXCLUIR"}', erros: [{ status: 400, codigos: ['confirmacao_invalida'], quando: 'A confirmação não é `EXCLUIR`.' }] }, corpo: '' }],
  ['excluirLocal', { arquivo: 'excluirLocal.md', frontmatter: { operacao: 'excluirLocal', proposta: 'Exclui o local.', acesso: 'dono', verificar: 'Veja o `204`.', caminho: { id: '00000000-0000-4000-8000-000000000001' }, consulta: { motivo: 'Duplicado' } }, corpo: '' }],
  ['obterCondicoes', { arquivo: 'obterCondicoes.md', frontmatter: { operacao: 'obterCondicoes', proposta: 'Devolve clima e maré.', acesso: 'publico', verificar: 'Veja `tempo`.', consulta: { lat: -23.55, lon: -46.63 }, erros: [{ status: 400, codigos: ['local_sem_coordenadas'], quando: 'Faltam `lat`/`lon`.' }] }, corpo: '' }],
  ['enviarNewsletter', { arquivo: 'enviarNewsletter.md', frontmatter: { operacao: 'enviarNewsletter', proposta: 'Dispara o envio.', acesso: 'admin', verificar: 'Veja o total de enviados.' }, corpo: '' }],
  ['criarPescaria', { arquivo: 'criarPescaria.md', frontmatter: { operacao: 'criarPescaria', proposta: 'Cria a pescaria.', acesso: 'sessao', verificar: 'Veja o header `Location`.', corpo: '{"local":"Represa de Guarapiranga","ambiente":"Represa","data":"2026-11-21","periodo":"Manha"}', erros: [{ status: 400, codigos: ['data_invalida'], quando: 'A data é inválida.' }] }, corpo: '' }],
  ['listarSessoes', { arquivo: 'listarSessoes.md', frontmatter: { operacao: 'listarSessoes', proposta: 'Lista as sessões.', acesso: 'sessao', verificar: 'Veja a lista.' }, corpo: '' }],
  ['buscarLocais', { arquivo: 'buscarLocais.md', frontmatter: { operacao: 'buscarLocais', proposta: 'Busca locais.', acesso: 'publico', verificar: 'Veja a lista.', consulta: { q: 'Guarapiranga' } }, corpo: '' }],
  ['enviarMidia', { arquivo: 'enviarMidia.md', frontmatter: { operacao: 'enviarMidia', proposta: 'Envia a foto.', acesso: 'sessao', verificar: 'Veja a URL.', arquivos: { arquivo: 'foto.jpg' }, erros: [{ status: 400, codigos: ['midia_grande'], quando: 'A foto passa de 5 MB.' }] }, corpo: '' }]
]);

const OPERACOES_DA_FIXTURE = [
  'obterSaude', 'excluirConta', 'excluirLocal', 'obterCondicoes', 'enviarNewsletter',
  'criarPescaria', 'listarSessoes', 'buscarLocais', 'enviarMidia'
];

const SERVIDOR = 'https://api.fisga.exemplo.tech';

function operacaoDaFixture(operationId) {
  return listarOperacoes(CONTRATO).find((item) => item.operationId === operationId);
}

function semChaves(frontmatter, ...chaves) {
  const copia = structuredClone(frontmatter);
  for (const chave of chaves) delete copia[chave];
  return copia;
}

// Valida uma variação do override da fixture: `mudancas` entra por cima do
// frontmatter original, `corpo` (quando informado) substitui o corpo do arquivo.
function validarDaFixture(operationId, mudancas = {}, corpo) {
  return validarFrontmatterDaFixture(operationId,
    { ...structuredClone(OVERRIDES.get(operationId).frontmatter), ...mudancas }, corpo);
}

// Variante que troca o frontmatter inteiro, para testar chave ausente.
function validarFrontmatterDaFixture(operationId, frontmatter, corpo) {
  const base = OVERRIDES.get(operationId);
  return validarOverride(
    { arquivo: base.arquivo, frontmatter, corpo: corpo === undefined ? base.corpo : corpo },
    operacaoDaFixture(operationId),
    CONTRATO);
}

function overrideDaFixture(operationId) {
  return OVERRIDES.get(operationId);
}

const SLUGS_NA_ORDEM_CANONICA = [
  'sistema', 'catalogo', 'autenticacao', 'conta', 'pescarias', 'diario',
  'condicoes', 'locais', 'pontos', 'trilhas', 'midia', 'especies', 'newsletter'
];

test('slug sem acento e minúsculo', () => {
  assert.equal(slugDoDominio('Autenticação'), 'autenticacao');
  assert.equal(slugDoDominio('Espécies'), 'especies');
  assert.equal(slugDoDominio('Condições'), 'condicoes');
});

test('ordem canônica cobre as 13 tags do contrato real', () => {
  assert.deepEqual(DOMINIOS.map((dominio) => dominio.slug), SLUGS_NA_ORDEM_CANONICA);

  const contrato = lerContrato();
  // O caminho explícito (URL) aponta para o mesmo contrato que o padrão.
  assert.deepEqual(lerContrato(new URL('../../docs/api/openapi.json', import.meta.url)), contrato);
  const operacoes = listarOperacoes(contrato);

  // Inventário medido do contrato: 48 caminhos, 54 operações, id único.
  assert.equal(Object.keys(contrato.paths).length, 48);
  assert.equal(operacoes.length, 54);
  assert.equal(new Set(operacoes.map((item) => item.operationId)).size, 54);

  // Toda tag do contrato está na ordem canônica e vice-versa; cada operação
  // carrega exatamente uma tag, e a distribuição por domínio é a medida.
  const tagsDoContrato = contrato.tags.map((tag) => tag.name).sort();
  const tagsDaOrdem = DOMINIOS.map((dominio) => dominio.tag).sort();
  assert.deepEqual(tagsDoContrato, tagsDaOrdem);
  assert.ok(operacoes.every((item) => tagsDaOrdem.includes(item.tag)));
  assert.ok(operacoes.every((item) => item.operacao.tags.length === 1));
  assert.equal(contrato.tags.length, 13);
  assert.equal(new Set(operacoes.map((item) => item.tag)).size, 13);

  const porDominio = DOMINIOS.map(
    (dominio) => operacoes.filter((item) => item.tag === dominio.tag).length);
  assert.deepEqual(porDominio, [3, 1, 16, 5, 6, 1, 2, 7, 4, 4, 1, 1, 3]);
  assert.equal(porDominio.reduce((total, n) => total + n, 0), 54);
});

test('operação derivada traz método, rota e domínio', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);

  const saude = modelo.operacoes.obterSaude;
  assert.equal(saude.metodo, 'GET');
  assert.equal(saude.rota, '/health');
  assert.equal(saude.dominio, 'sistema');

  // `info` carrega a lista inteira de servidores, não só o primeiro.
  assert.deepEqual(modelo.info.servidores, [
    { url: 'https://api.fisga.exemplo.tech', descricao: 'Produção' }
  ]);
  assert.equal(modelo.info.servidor, 'https://api.fisga.exemplo.tech');
  assert.equal(modelo.info.titulo, 'API do Fisga');
  assert.equal(modelo.info.versao, '1.0.0');

  // O domínio lista os operationId na ordem de `paths`.
  const locais = modelo.dominios.find((dominio) => dominio.slug === 'locais');
  assert.equal(locais.tag, 'Locais');
  assert.equal(locais.titulo, 'Locais');
  assert.equal(locais.descricao, 'Descrição de Locais.');
  assert.equal(locais.total, 2);
  assert.deepEqual(locais.operacoes, ['excluirLocal', 'buscarLocais']);
});

test('autenticação vem de security', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);

  // A forma legível, que a página mostra em "Autenticação: ...".
  assert.equal(modelo.operacoes.obterSaude.autenticacao, 'nenhuma');
  assert.equal(modelo.operacoes.obterSaude.autenticacaoTexto, 'sem autenticação');

  assert.equal(modelo.operacoes.excluirConta.autenticacao, 'sessao');
  assert.equal(
    modelo.operacoes.excluirConta.autenticacaoTexto, 'cookie de sessão `fisga_auth`');

  assert.equal(modelo.operacoes.enviarNewsletter.autenticacao, 'admin');
  assert.equal(
    modelo.operacoes.enviarNewsletter.autenticacaoTexto, 'header `X-Admin-Token`');

  // No contrato real: 23 sem autenticação, 29 com cookie, 2 com token.
  const operacoes = listarOperacoes(lerContrato());
  const contar = (valor) =>
    operacoes.filter((item) => (item.operacao.security ?? []).length === valor).length;
  const porEsquema = (esquema) => operacoes.filter(
    (item) => JSON.stringify(item.operacao.security) === JSON.stringify([{ [esquema]: [] }])).length;
  assert.equal(contar(0), 23);
  assert.equal(porEsquema('cookieAuth'), 29);
  assert.equal(porEsquema('adminToken'), 2);

  // Forma composta não existe no contrato e vira pendência, não palpite.
  const composta = structuredClone(CONTRATO);
  composta.paths['/health'].get.security = [{ cookieAuth: [], adminToken: [] }];
  assert.throws(() => gerarModelo(composta, OVERRIDES), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    assert.ok(erro.problemas.some(
      (problema) => problema.includes('obterSaude') && problema.includes('security não reconhecido')),
    erro.problemas.join(' | '));
    return true;
  });
});

test('consentimento é cookieAuth com 403 de consentimento', () => {
  assert.ok(derivarConsentimento(CONTRATO).has('excluirConta'));

  // Contrato real: 28 das 29 com cookie. A única fora do conjunto é a rota que
  // registra o próprio consentimento; as duas da allowlist do middleware têm
  // `security` vazio e por isso nem entram na conta.
  const consentimento = derivarConsentimento(lerContrato());
  assert.equal(consentimento.size, 28);
  for (const operationId of ['registrarConsentimento', 'obterSessao', 'sair']) {
    assert.equal(consentimento.has(operationId), false, operationId);
  }

  // Cada operação do modelo carrega o booleano e, quando verdadeiro, o texto.
  const modelo = gerarModelo(CONTRATO, OVERRIDES);
  for (const operacao of Object.values(modelo.operacoes)) {
    assert.equal(typeof operacao.consentimento, 'boolean');
    if (operacao.consentimento) {
      assert.equal(typeof operacao.consentimentoTexto, 'string');
      assert.ok(operacao.consentimentoTexto.trim().length > 0);
    } else {
      assert.equal(operacao.consentimentoTexto, null);
    }
  }
  assert.equal(modelo.operacoes.excluirConta.consentimento, true);
  assert.equal(modelo.operacoes.obterSaude.consentimento, false);
});

test('erros derivados: codigo nomeado e corpo vazio; 400 com corpo fica pendente', () => {
  const operacao = CONTRATO.paths['/api/conta'].delete;
  const erros = derivarErros(operacao);

  assert.deepEqual(erros.map((erro) => erro.status), [401, 403, 429]);
  assert.deepEqual(erros.map((erro) => erro.codigos), [
    ['nao_autenticado'],
    ['acesso_negado', 'consentimento_pendente'],
    null
  ]);
  assert.ok(erros.every((erro) => erro.origem === 'contrato'));

  // O `quando` é a `description` do contrato, verbatim e trimada.
  assert.equal(erros[0].quando, 'Sessão ausente (`nao_autenticado`).');
  assert.equal(erros[2].quando,
    'Muitas requisições. Corpo vazio; o header `Retry-After` informa os segundos.');

  // O `400` tem corpo e a descrição não nomeia código: fica para o override.
  assert.equal(erros.some((erro) => erro.status === 400), false);
});

test('no contrato real, 115 pares derivam e 37 ficam pendentes', () => {
  const { derivados, pendencias } = auditar(lerContrato());

  assert.equal(derivados, 115);
  assert.equal(pendencias.length, 37);

  const porStatus = {};
  for (const pendencia of pendencias) {
    porStatus[pendencia.status] = (porStatus[pendencia.status] ?? 0) + 1;
  }
  assert.deepEqual(porStatus, { 400: 23, 404: 12, 503: 2 });

  const porOperacao = new Map();
  for (const pendencia of pendencias) {
    porOperacao.set(pendencia.operationId, (porOperacao.get(pendencia.operationId) ?? 0) + 1);
  }
  assert.equal(porOperacao.size, 33);

  const comDuas = [...porOperacao].filter(([, total]) => total === 2)
    .map(([operationId]) => operationId).sort();
  assert.deepEqual(comDuas,
    ['atualizarLocal', 'atualizarPescaria', 'atualizarPonto', 'atualizarTrilha']);

  // Essas 4 vêm com os dois status na ordem numérica, que é a ordem em que o
  // override precisa cobri-los: o `400` de validação e o `404` de recurso.
  for (const operationId of comDuas) {
    assert.deepEqual(
      pendencias.filter((pendencia) => pendencia.operationId === operationId)
        .map((pendencia) => pendencia.status),
      [400, 404], operationId);
  }
});

test('limites e cache vêm do contrato; ausência vira null', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);

  const limites = modelo.operacoes.excluirConta.limites;
  assert.equal(limites.policy, 'escrita');
  assert.equal(limites.requests, 40);
  assert.equal(limites.windowSeconds, 60);

  assert.equal(modelo.operacoes.obterSaude.limites, null);
  assert.equal(modelo.operacoes.obterSaude.xCache, 'private, no-store');
  assert.equal(modelo.operacoes.excluirConta.xCache, 'private, no-store');
  assert.equal(modelo.operacoes.obterCondicoes.xCache, 'public, max-age=300');

  // Contrato real: 4 operações sem `x-rate-limit`, 54 com `x-cache` e 7 valores.
  const operacoes = listarOperacoes(lerContrato());
  const semLimite = operacoes.filter((item) => item.operacao['x-rate-limit'] === undefined)
    .map((item) => item.operationId).sort();
  assert.deepEqual(semLimite,
    ['enviarNewsletter', 'obterProntidao', 'obterSaude', 'previsualizarNewsletter']);
  assert.equal(operacoes.filter((item) => item.operacao['x-cache'] !== undefined).length, 54);
  assert.equal(new Set(operacoes.map((item) => item.operacao['x-cache'])).size, 7);
});

test('parâmetros preservam local e obrigatoriedade', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);

  const parametros = modelo.operacoes.excluirLocal.parametros;
  assert.equal(parametros.length, 2);
  assert.equal(parametros[0].nome, 'id');
  assert.equal(parametros[0].local, 'path');
  assert.equal(parametros[0].obrigatorio, true);
  assert.equal(parametros[0].tipo, 'string (uuid)');
  assert.equal(parametros[1].nome, 'motivo');
  assert.equal(parametros[1].local, 'query');
  assert.equal(parametros[1].obrigatorio, false);

  // `type` composto com `string` vira `string` (o valor chega como texto na
  // query), e o `format` entra entre parênteses; sem `format`, só o tipo.
  const condicoes = modelo.operacoes.obterCondicoes.parametros;
  assert.deepEqual(condicoes.map((parametro) => parametro.nome), ['lat', 'lon']);
  assert.ok(condicoes.every((parametro) => parametro.obrigatorio === false));
  assert.ok(condicoes.every((parametro) => parametro.tipo === 'string (double)'));

  const busca = modelo.operacoes.buscarLocais.parametros;
  assert.equal(busca.length, 1);
  assert.equal(busca[0].nome, 'q');
  assert.equal(busca[0].local, 'query');
  assert.equal(busca[0].tipo, 'string');
  assert.equal(busca[0].obrigatorio, false);

  // Contrato real: 12 path params, 19 query params, nenhum query obrigatório.
  const operacoes = listarOperacoes(lerContrato());
  const todos = operacoes.flatMap((item) => item.operacao.parameters ?? []);
  assert.equal(todos.filter((parametro) => parametro.in === 'path').length, 12);
  const consulta = todos.filter((parametro) => parametro.in === 'query');
  assert.equal(consulta.length, 19);
  assert.ok(consulta.every((parametro) => parametro.required !== true));
});

test('tag fora de DOMINIOS falha', () => {
  const contrato = { ...CONTRATO, tags: [...CONTRATO.tags, { name: 'Inventada', description: '' }] };
  assert.throws(() => gerarModelo(contrato, OVERRIDES), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    assert.ok(erro.problemas.some((problema) => problema.includes('Inventada')), erro.problemas.join(' | '));
    return true;
  });
});

test('domínio da ordem canônica sem tag no contrato falha', () => {
  const contrato = { ...CONTRATO, tags: CONTRATO.tags.filter((tag) => tag.name !== 'Conta') };
  assert.throws(() => gerarModelo(contrato, OVERRIDES), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    assert.ok(erro.problemas.some((problema) => problema.includes('Conta')), erro.problemas.join(' | '));
    return true;
  });
});

test('operação sem override falha com o operationId', () => {
  assert.throws(() => gerarModelo(CONTRATO, new Map()), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    for (const operationId of OPERACOES_DA_FIXTURE) {
      assert.ok(
        erro.problemas.some((problema) => problema.includes(operationId)),
        `faltou ${operationId} em ${erro.problemas.join(' | ')}`);
    }
    return true;
  });
});

test('override de operação inexistente falha', () => {
  const overrides = new Map([
    ...OVERRIDES,
    ['naoExiste', { arquivo: 'naoExiste.md', frontmatter: {}, corpo: '' }]
  ]);
  assert.throws(() => gerarModelo(CONTRATO, overrides), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    assert.ok(erro.problemas.some((problema) => problema.includes('naoExiste')), erro.problemas.join(' | '));
    return true;
  });
});

test('override sem corpo deixa notasHtml vazio', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);
  const saude = modelo.operacoes.obterSaude;

  assert.equal(saude.notas, '');
  assert.equal(saude.notasHtml, '');
});

test('override entra no modelo com notas renderizadas e valores do exemplo', () => {
  const overrides = new Map(OVERRIDES);
  overrides.set('obterSaude', {
    arquivo: 'obterSaude.md',
    frontmatter: {
      ...OVERRIDES.get('obterSaude').frontmatter,
      relacionados: ['obterCondicoes', 'excluirLocal']
    },
    corpo: 'Uma nota com **ênfase**.'
  });

  const modelo = gerarModelo(CONTRATO, overrides);
  const saude = modelo.operacoes.obterSaude;

  assert.equal(saude.proposta, 'Diz se está no ar.');
  assert.equal(saude.acesso, 'publico');
  assert.equal(saude.acessoTexto, 'qualquer pessoa, sem autenticação');
  assert.equal(saude.consentimentoTexto, null);
  assert.equal(saude.verificar, 'Veja `status`.');
  assert.equal(saude.notas, 'Uma nota com **ênfase**.');
  assert.equal(saude.notasHtml, '<p>Uma nota com <strong>ênfase</strong>.</p>\n');
  assert.deepEqual(saude.relacionados, [
    { operationId: 'obterCondicoes', titulo: 'Condições', dominio: 'condicoes' },
    { operationId: 'excluirLocal', titulo: 'Exclui o local', dominio: 'locais' }
  ]);
  assert.deepEqual(saude.exemplo,
    ['curl -s', `"${SERVIDOR}/health"`]);

  // R1: o corpo mora em `request`, não no nível da operação.
  assert.deepEqual(saude.request, { corpoExemplo: null, arquivos: null });
  assert.equal(saude.corpo, undefined);
  assert.equal(saude.arquivos, undefined);

  const conta = modelo.operacoes.excluirConta;
  assert.equal(conta.acessoTexto, 'qualquer pessoa com sessão iniciada');
  assert.ok(conta.consentimentoTexto.includes('consentimento_pendente'));
  assert.deepEqual(conta.request, {
    corpoExemplo: '{"confirmacao":"EXCLUIR"}',
    arquivos: null
  });
  assert.ok(conta.exemplo.includes(`-d '{"confirmacao":"EXCLUIR"}'`), conta.exemplo);
  assert.ok(conta.exemplo.includes('-b cookies.txt'), conta.exemplo);

  const midia = modelo.operacoes.enviarMidia;
  assert.deepEqual(midia.request, { corpoExemplo: null, arquivos: { arquivo: 'foto.jpg' } });
  assert.ok(midia.exemplo.includes('-F "arquivo=@foto.jpg"'), midia.exemplo);

  // `consentimentoTexto` é derivado do contrato, não do override.
  assert.equal(midia.consentimento, false);
  assert.equal(midia.consentimentoTexto, null);

  // Parâmetros carregam o valor concreto do override.
  assert.deepEqual(
    modelo.operacoes.obterCondicoes.parametros.map((parametro) => parametro.valor),
    [-23.55, -46.63]);
  assert.deepEqual(
    modelo.operacoes.excluirLocal.parametros.map((parametro) => parametro.valor),
    ['00000000-0000-4000-8000-000000000001', 'Duplicado']);
  assert.deepEqual(
    modelo.operacoes.obterSaude.parametros, []);
});

test('erros do modelo com codigos vazio explicito viram null', () => {
  const overrides = new Map(OVERRIDES);
  overrides.set('excluirConta', {
    ...OVERRIDES.get('excluirConta'),
    frontmatter: {
      ...OVERRIDES.get('excluirConta').frontmatter,
      erros: [{ status: 400, codigos: [], quando: 'O corpo é recusado antes do handler.' }]
    }
  });

  const conta = gerarModelo(CONTRATO, overrides).operacoes.excluirConta;
  assert.equal(conta.erros[0].status, 400);
  // R27: a página renderiza `—`; `[]` não pode vazar para o JSON.
  assert.equal(conta.erros[0].codigos, null);
});

test('erros do modelo somam derivados e override, ordenados por status', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);
  const conta = modelo.operacoes.excluirConta;

  assert.deepEqual(conta.erros.map((erro) => erro.status), [400, 401, 403, 429]);
  assert.deepEqual(conta.erros.map((erro) => erro.origem),
    ['override', 'contrato', 'contrato', 'contrato']);
  assert.deepEqual(conta.erros[0].codigos, ['confirmacao_invalida']);
  // R7: o `quando` derivado é a `description` do contrato, verbatim.
  assert.equal(conta.erros[1].quando, 'Sessão ausente (`nao_autenticado`).');
  assert.equal(conta.erros[3].codigos, null);

  // `enviarNewsletter`: o 404 e o 500 derivam, e o override não acrescenta nada.
  const newsletter = modelo.operacoes.enviarNewsletter;
  assert.deepEqual(newsletter.erros.map((erro) => erro.status), [404, 500]);
  assert.ok(newsletter.erros.every((erro) => erro.origem === 'contrato'));
});

test('override inválido falha a geração com o arquivo e o campo', () => {
  const overrides = new Map(OVERRIDES);
  overrides.set('obterSaude', {
    arquivo: 'obterSaude.md',
    frontmatter: { ...OVERRIDES.get('obterSaude').frontmatter, resposta: 'x' },
    corpo: ''
  });

  assert.throws(() => gerarModelo(CONTRATO, overrides), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    assert.ok(erro.problemas.some(
      (problema) => problema.includes('obterSaude.md') &&
        problema.includes('chave desconhecida: resposta')),
    erro.problemas.join(' | '));
    return true;
  });
});

test('contagem é calculada, não literal', () => {
  const modelo = gerarModelo(CONTRATO, OVERRIDES);
  assert.deepEqual(modelo.contagem, { operacoes: 9, dominios: 13, paginas: 24 });
});

test('mensagem de pendência segue "<arquivo ou operação>: <campo>: <motivo>"', () => {
  assert.throws(() => gerarModelo(CONTRATO, new Map()), (erro) => {
    assert.ok(erro.problemas.length >= OPERACOES_DA_FIXTURE.length);
    for (const problema of erro.problemas) {
      assert.match(problema, /^[^:]+: [^:]+: \S/);
    }
    return true;
  });
});

test('importar o módulo não executa main', () => {
  // A guarda `chamadoDiretamente()` é o que impede o teste de rodar o CLI: sem
  // ela, este import executaria `main()`, imprimiria as operações sem override
  // e sairia com 1, derrubando `execFileSync` em vez de devolver string vazia.
  const diretorio = fileURLToPath(new URL('../', import.meta.url));
  const saida = execFileSync(
    process.execPath,
    ['--input-type=module', '-e', "await import('./scripts/gerar-referencia.mjs')"],
    { cwd: diretorio, encoding: 'utf8', stdio: 'pipe' });
  assert.equal(saida, '');
});

test('os overrides da fixture passam na validação', () => {
  for (const operationId of OPERACOES_DA_FIXTURE) {
    assert.deepEqual(validarDaFixture(operationId), [], operationId);
  }
});

test('frontmatter com chave desconhecida falha', () => {
  const problemas = validarDaFixture('obterSaude', { resposta: 'x' });
  assert.ok(problemas.some((problema) => problema.includes('chave desconhecida: resposta')),
    problemas.join(' | '));
});

test('corpo com heading falha, inclusive indentado', () => {
  const problemas = validarDaFixture('obterSaude', {}, '# Título\n\nTexto.');
  assert.ok(problemas.some((problema) => problema.includes('heading')), problemas.join(' | '));

  // `marked` aceita até três espaços antes do `#` e ainda renderiza `<h1>`.
  const indentado = validarDaFixture('obterSaude', {}, '   # Título\n\nTexto.');
  assert.ok(indentado.some((problema) => problema.includes('heading')),
    indentado.join(' | '));

  // HTML cru também vira heading na renderização.
  const html = validarDaFixture('obterSaude', {}, '   <h1>Título</h1>\n\nTexto.');
  assert.ok(html.some((problema) => problema.includes('heading')), html.join(' | '));

  // Heading dentro de bloco de código é conteúdo, não seção.
  assert.deepEqual(
    validarDaFixture('obterSaude', {}, 'Veja:\n\n```bash\n# comentário\n```'),
    []);
});

test('corpo com <script> falha (R28)', () => {
  const problemas = validarDaFixture('obterSaude', {}, 'Texto <SCRIPT src="x.js"></SCRIPT>');
  assert.ok(problemas.some((problema) => problema.includes('<script')),
    problemas.join(' | '));

  // HTML inofensivo continua livre.
  assert.deepEqual(validarDaFixture('obterSaude', {}, 'Texto <strong>forte</strong>.'), []);
});

test('status derivável no override falha', () => {
  const problemas = validarDaFixture('excluirConta', {
    erros: [
      { status: 400, codigos: ['confirmacao_invalida'], quando: 'A confirmação não é `EXCLUIR`.' },
      { status: 401, codigos: ['nao_autenticado'], quando: 'Sem sessão.' }
    ]
  });
  assert.ok(problemas.some((problema) => problema.includes('linha derivável do contrato')),
    problemas.join(' | '));
});

test('status >= 400 pendente sem linha falha', () => {
  const problemas = validarFrontmatterDaFixture('excluirConta',
    semChaves(OVERRIDES.get('excluirConta').frontmatter, 'erros'));
  assert.ok(problemas.some((problema) => problema.includes('400')), problemas.join(' | '));
});

test('status que o contrato não declara falha', () => {
  const problemas = validarDaFixture('excluirConta', {
    erros: [
      { status: 400, codigos: ['confirmacao_invalida'], quando: 'A confirmação não é `EXCLUIR`.' },
      { status: 418, codigos: ['sou_um_bule'], quando: 'Sou um bule.' }
    ]
  });
  assert.ok(problemas.some((problema) => problema.includes('418')), problemas.join(' | '));
  assert.ok(problemas.some((problema) => problema.includes('status não declarado')), problemas.join(' | '));
});

test('dois erros com o mesmo status falham', () => {
  const problemas = validarDaFixture('excluirConta', {
    erros: [
      { status: 400, codigos: ['confirmacao_invalida'], quando: 'A confirmação não é `EXCLUIR`.' },
      { status: 400, codigos: ['confirmacao_invalida'], quando: 'Repetido.' }
    ]
  });
  assert.ok(problemas.some((problema) => problema.includes('status repetido')),
    problemas.join(' | '));
});

test('R27: codigos vazio explícito vale, mas ausente ou contraditório falha', () => {
  // `[]` é a decisão explícita de "este status não tem código nomeado" — é o
  // caso de `entrarComSenha`/`esquecerSenha`, cujo 400 nunca nomeia código.
  assert.deepEqual(validarDaFixture('excluirConta', {
    erros: [{ status: 400, codigos: [], quando: 'A confirmação não é `EXCLUIR`.' }]
  }), []);

  // Sem `codigos` nem `codigo` o autor não decidiu nada: continua problema.
  const ausente = validarDaFixture('excluirConta', {
    erros: [{ status: 400, quando: 'A confirmação não é `EXCLUIR`.' }]
  });
  assert.ok(ausente.some((problema) => problema.includes('codigos precisa ser lista')),
    ausente.join(' | '));

  const contradicao = validarDaFixture('excluirConta', {
    erros: [{
      status: 400,
      codigos: [],
      codigo: 'confirmacao_invalida',
      quando: 'A confirmação não é `EXCLUIR`.'
    }]
  });
  assert.ok(contradicao.some((problema) => problema.includes('contradiz')),
    contradicao.join(' | '));

  // O atalho `codigo` continua valendo e `codigo` vazio continua recusado.
  assert.deepEqual(validarDaFixture('excluirConta', {
    erros: [{ status: 400, codigo: 'confirmacao_invalida', quando: 'A confirmação não é `EXCLUIR`.' }]
  }), []);
  const vazio = validarDaFixture('excluirConta', {
    erros: [{ status: 400, codigo: '', quando: 'A confirmação não é `EXCLUIR`.' }]
  });
  assert.ok(vazio.some((problema) => problema.includes('codigos precisa ser lista')),
    vazio.join(' | '));

  // `codigos: []` não absolve um `codigo` vazio declarado ao lado.
  const ambos = validarDaFixture('excluirConta', {
    erros: [{ status: 400, codigos: [], codigo: '', quando: 'A confirmação não é `EXCLUIR`.' }]
  });
  assert.ok(ambos.some((problema) => problema.includes('codigo precisa ser string')),
    ambos.join(' | '));
});

test('as duas operações sem código nomeado aceitam codigos vazio no contrato real', () => {
  const contrato = lerContrato();
  const itens = new Map(listarOperacoes(contrato).map((item) => [item.operationId, item]));
  const casos = [
    ['entrarComSenha', '{"email":"pescador@exemplo.tech","senha":"senha-de-exemplo"}'],
    ['esquecerSenha', '{"email":"pescador@exemplo.tech"}']
  ];

  for (const [operationId, corpo] of casos) {
    const problemas = validarOverride({
      arquivo: `${operationId}.md`,
      corpo: '',
      frontmatter: {
        operacao: operationId,
        proposta: 'Diz o que faz.',
        acesso: 'publico',
        verificar: 'Veja o status.',
        corpo,
        erros: [{ status: 400, codigos: [], quando: 'Recusado antes do handler, sem corpo.' }]
      }
    }, itens.get(operationId), contrato);
    assert.deepEqual(problemas, [], operationId);
  }
});

test('arquivos com campo fora do schema ou faltando required falha', () => {
  const foraDoSchema = validarDaFixture('enviarMidia', {
    arquivos: { arquivo: 'foto.jpg', extra: 'outra.jpg' }
  });
  assert.ok(foraDoSchema.some((problema) => problema.includes('campo fora do schema: extra')),
    foraDoSchema.join(' | '));

  const faltando = validarDaFixture('enviarMidia', { arquivos: { outro: 'foto.jpg' } });
  assert.ok(faltando.some((problema) => problema.includes('falta o campo arquivo')),
    faltando.join(' | '));
});

test('acesso com chave de protótipo recebe a mensagem de enum', () => {
  const problemas = validarDaFixture('obterSaude', { acesso: 'toString' });
  assert.deepEqual(problemas, [
    'obterSaude.md: acesso: precisa ser um de publico, capacidade, sessao, dono, admin'
  ]);
});

test('JSON sem corpo falha e multipart sem arquivos falha', () => {
  const semCorpo = validarFrontmatterDaFixture('excluirConta', semChaves(
    OVERRIDES.get('excluirConta').frontmatter, 'corpo'));
  const semArquivos = validarFrontmatterDaFixture('enviarMidia', semChaves(
    OVERRIDES.get('enviarMidia').frontmatter, 'arquivos'));

  assert.ok(semCorpo.some((problema) => problema.includes('corpo')), semCorpo.join(' | '));
  assert.ok(semArquivos.some((problema) => problema.includes('arquivos')), semArquivos.join(' | '));
  assert.notEqual(semCorpo.join(' '), semArquivos.join(' '));
});

test('EntradaDePescaria sem required aceita corpo parcial, mas o exemplo ainda precisa ser objeto válido', () => {
  assert.deepEqual(validarDaFixture('criarPescaria', { corpo: '{"ambiente":"Represa"}' }), []);

  const problemas = validarDaFixture('criarPescaria', { corpo: '[]' });
  assert.ok(problemas.some((problema) => problema.includes('corpo precisa ser objeto')),
    problemas.join(' | '));
});

test('consulta obrigatória de obterCondicoes é exigida pelo override, não pelo contrato', () => {
  const problemas = validarFrontmatterDaFixture('obterCondicoes', semChaves(
    OVERRIDES.get('obterCondicoes').frontmatter, 'consulta'));
  assert.ok(problemas.some((problema) => problema.includes('consulta')), problemas.join(' | '));

  // O contrato não marca `lat`/`lon` como obrigatórios; quem exige valor é o
  // override, porque sem coordenada a resposta é `400`.
  const parametros = listarOperacoes(lerContrato())
    .find((item) => item.operationId === 'obterCondicoes').operacao.parameters;
  for (const nome of ['lat', 'lon']) {
    assert.notEqual(parametros.find((parametro) => parametro.name === nome).required, true, nome);
  }
});

test('corpo presente sem requestBody falha', () => {
  const problemas = validarDaFixture('obterSaude', { corpo: '{"x":1}' });
  assert.ok(problemas.some((problema) => problema.includes('corpo')), problemas.join(' | '));
});

test('caminho obrigatório quando a rota tem path param', () => {
  const semCaminho = validarFrontmatterDaFixture('excluirLocal', semChaves(
    OVERRIDES.get('excluirLocal').frontmatter, 'caminho'));
  assert.ok(semCaminho.some((problema) => problema.includes('caminho')), semCaminho.join(' | '));

  const chaveErrada = validarDaFixture('excluirLocal', {
    caminho: { outro: '00000000-0000-4000-8000-000000000001' }
  });
  assert.ok(chaveErrada.some((problema) => problema.includes('chave fora de parameters')),
    chaveErrada.join(' | '));

  const uuidInvalido = validarDaFixture('excluirLocal', { caminho: { id: '42' } });
  assert.ok(uuidInvalido.some((problema) => problema.includes('UUID')), uuidInvalido.join(' | '));
});

test('caminho presente em rota sem path param falha uma vez só', () => {
  const problemas = validarDaFixture('obterSaude', {
    caminho: { id: '00000000-0000-4000-8000-000000000001' }
  });
  const doCaminho = problemas.filter((problema) => problema.includes('caminho:'));
  assert.deepEqual(doCaminho, [
    'obterSaude.md: caminho: proibido porque a rota não tem parâmetro de caminho'
  ], problemas.join(' | '));
});

test('caminho e consulta nulos geram uma mensagem por chave', () => {
  for (const [operationId, campo] of [
    ['excluirLocal', 'caminho'],
    ['obterCondicoes', 'consulta']
  ]) {
    const problemas = validarDaFixture(operationId, { [campo]: null });
    const doCampo = problemas.filter((problema) => problema.includes(`${campo}:`));
    assert.equal(doCampo.length, 1, problemas.join(' | '));
    assert.ok(doCampo[0].includes('precisa ser objeto'), problemas.join(' | '));
  }
});

test('valor nulo ou vazio em caminho e consulta falha', () => {
  const nulo = validarDaFixture('excluirLocal', { caminho: { id: null } });
  assert.ok(nulo.some((problema) => problema.includes('caminho: id:')),
    nulo.join(' | '));

  const vazio = validarDaFixture('buscarLocais', { consulta: { q: '' } });
  assert.ok(vazio.some((problema) => problema.includes('consulta: q:')),
    vazio.join(' | '));
});

test('consulta com chave fora de parameters falha', () => {
  const problemas = validarDaFixture('buscarLocais', { consulta: { termo: 'Guarapiranga' } });
  assert.ok(problemas.some((problema) => problema.includes('consulta')), problemas.join(' | '));
  assert.ok(problemas.some((problema) => problema.includes('chave fora de parameters')),
    problemas.join(' | '));
});

test('acesso incompatível com security falha', () => {
  const sessaoEmRotaPublica = validarDaFixture('obterSaude', { acesso: 'sessao' });
  assert.ok(sessaoEmRotaPublica.some((problema) => problema.includes('acesso')),
    sessaoEmRotaPublica.join(' | '));

  const publicoEmRotaDeSessao = validarDaFixture('listarSessoes', { acesso: 'publico' });
  assert.ok(publicoEmRotaDeSessao.some((problema) => problema.includes('acesso')),
    publicoEmRotaDeSessao.join(' | '));

  const capacidadeSemToken = validarDaFixture('obterSaude', { acesso: 'capacidade' });
  assert.ok(capacidadeSemToken.some((problema) =>
    problema.includes('acesso') && problema.includes('token')), capacidadeSemToken.join(' | '));

  const adminEmRotaDeSessao = validarDaFixture('excluirConta', { acesso: 'admin' });
  assert.ok(adminEmRotaDeSessao.some((problema) => problema.includes('acesso')),
    adminEmRotaDeSessao.join(' | '));
});

test('relacionado inexistente falha', () => {
  const problemas = validarDaFixture('obterSaude', { relacionados: ['naoExiste'] });
  assert.ok(problemas.some((problema) => problema.includes('naoExiste')), problemas.join(' | '));
});

test('operacao do frontmatter precisa ser a operação da página', () => {
  const outroArquivo = validarDaFixture('obterSaude', { operacao: 'excluirConta' });
  assert.ok(outroArquivo.some((problema) => problema.includes('operacao')), outroArquivo.join(' | '));

  const outroContrato = validarDaFixture('obterSaude', { operacao: 'naoExiste' });
  assert.ok(outroContrato.some((problema) => problema.includes('naoExiste')), outroContrato.join(' | '));
});

test('lerOverrides lê frontmatter e corpo, e ignora diretório ausente', () => {
  assert.deepEqual(lerOverrides(new URL('./nao/existe/', import.meta.url)), []);
  // Caminho em texto também é aceito, como em `lerContrato`.
  assert.deepEqual(lerOverrides(join(tmpdir(), 'overrides-nao-existem')), []);

  const diretorio = mkdtempSync(join(tmpdir(), 'overrides-'));
  try {
    writeFileSync(join(diretorio, 'obterSaude.md'), [
      '---',
      'operacao: obterSaude',
      'proposta: Diz se está no ar.',
      'acesso: publico',
      'verificar: Veja `status`.',
      '---',
      '',
      'Uma nota.',
      ''
    ].join('\n'));

    const [entrada] = lerOverrides(pathToFileURL(`${diretorio}/`));
    assert.equal(entrada.arquivo, 'obterSaude.md');
    assert.equal(entrada.operacao, 'obterSaude');
    assert.equal(entrada.frontmatter.acesso, 'publico');
    assert.equal(entrada.corpo, 'Uma nota.');
  } finally {
    rmSync(diretorio, { recursive: true, force: true });
  }
});

test('lerOverrides recusa arquivo sem frontmatter e nome fora de <operacao>.md', () => {
  const diretorio = mkdtempSync(join(tmpdir(), 'overrides-'));
  try {
    const comFrontmatter = (operacao) => [
      '---', `operacao: ${operacao}`, 'proposta: Diz.', 'acesso: publico',
      'verificar: Veja.', '---', ''
    ].join('\n');

    writeFileSync(join(diretorio, 'semFrontmatter.md'), '# só corpo\n');
    assert.throws(() => lerOverrides(pathToFileURL(`${diretorio}/`)), (erro) => {
      assert.ok(erro instanceof ErroDeGeracao);
      assert.ok(erro.problemas.some((problema) => problema.includes('frontmatter')),
        erro.problemas.join(' | '));
      return true;
    });

    rmSync(join(diretorio, 'semFrontmatter.md'));
    writeFileSync(join(diretorio, 'yamlQuebrado.md'), '---\noperacao: [\n---\n');
    assert.throws(() => lerOverrides(pathToFileURL(`${diretorio}/`)), (erro) => {
      assert.ok(erro instanceof ErroDeGeracao);
      assert.ok(erro.problemas.some((problema) => problema.includes('YAML inválido')),
        erro.problemas.join(' | '));
      return true;
    });

    rmSync(join(diretorio, 'yamlQuebrado.md'));
    writeFileSync(join(diretorio, 'outroNome.md'), comFrontmatter('obterSaude'));
    assert.throws(() => lerOverrides(pathToFileURL(`${diretorio}/`)), (erro) => {
      assert.ok(erro instanceof ErroDeGeracao);
      assert.ok(erro.problemas.some((problema) => problema.includes('nome do arquivo')),
        erro.problemas.join(' | '));
      return true;
    });
  } finally {
    rmSync(diretorio, { recursive: true, force: true });
  }
});

test('curl de sessão usa cookies.txt', () => {
  const linhas = montarCurl(
    operacaoDaFixture('listarSessoes'), overrideDaFixture('listarSessoes'), SERVIDOR);
  const curl = linhas.join('\n');

  assert.ok(curl.includes('-b cookies.txt'), linhas.join(' | '));
  assert.equal(curl.includes('X-Admin-Token'), false, linhas.join(' | '));
  assert.ok(linhas[0].startsWith('curl -s'), linhas.join(' | '));
});

test('curl administrativo usa X-Admin-Token', () => {
  const linhas = montarCurl(
    operacaoDaFixture('enviarNewsletter'), overrideDaFixture('enviarNewsletter'), SERVIDOR);

  assert.ok(linhas.some((linha) => linha.includes('-H "X-Admin-Token: SEU_ADMIN_TOKEN"')),
    linhas.join(' | '));
  assert.equal(linhas.join('\n').includes('cookies.txt'), false, linhas.join(' | '));
  assert.ok(linhas.some((linha) => linha.includes('-X POST')), linhas.join(' | '));
});

test('curl com path e query concretos', () => {
  const condicoes = montarCurl(
    operacaoDaFixture('obterCondicoes'), overrideDaFixture('obterCondicoes'), SERVIDOR);
  assert.ok(condicoes.some((linha) =>
    linha.includes(`?lat=-23.55&lon=-46.63`)), condicoes.join(' | '));
  // A ordem é a de `parameters` e só entra chave declarada na consulta.
  assert.equal(condicoes.join('\n').includes('nome='), false, condicoes.join(' | '));

  const local = montarCurl(
    operacaoDaFixture('excluirLocal'), overrideDaFixture('excluirLocal'), SERVIDOR);
  assert.ok(local.some((linha) =>
    linha.includes('/api/locais/00000000-0000-4000-8000-000000000001?motivo=Duplicado')),
  local.join(' | '));
  assert.ok(local.some((linha) => linha.includes('-X DELETE')), local.join(' | '));
  // `204` pede os headers na resposta.
  assert.ok(local[0].includes('-i'), local.join(' | '));
});

test('curl escapa aspas simples do corpo para o shell', () => {
  // `Praia d'água` existe no domínio: sem `'\''` o shell cortaria a string no
  // apóstrofo e o `-d` receberia JSON inválido.
  const override = {
    ...overrideDaFixture('excluirConta'),
    frontmatter: {
      ...overrideDaFixture('excluirConta').frontmatter,
      corpo: `{"confirmacao":"Praia d'água"}`
    }
  };
  const linhas = montarCurl(operacaoDaFixture('excluirConta'), override, SERVIDOR);
  const linha = linhas.find((candidata) => candidata.startsWith('-d '));

  assert.equal(linha, `-d '{"confirmacao":"Praia d'\\''água"}'`);
});

test('a origem da linha é decidida só pela description, não por content', () => {
  const casos = [
    { nomeiaCodigo: true, dizCorpoVazio: false, esperado: 'codigos' },
    { nomeiaCodigo: true, dizCorpoVazio: true, esperado: 'codigos' },
    { nomeiaCodigo: false, dizCorpoVazio: true, esperado: 'codigos-nulos' },
    { nomeiaCodigo: false, dizCorpoVazio: false, esperado: 'pendente' }
  ];
  const conteudos = [undefined, {}, { 'application/json': {} }];

  for (const caso of casos) {
    for (const content of conteudos) {
      const descricao = [
        caso.nomeiaCodigo && 'Sessão ausente (`nao_autenticado`).',
        caso.dizCorpoVazio && 'Corpo vazio.'
      ].filter(Boolean).join(' ') || 'Recurso não encontrado.';
      const resposta = content === undefined ? { description: descricao } : { description: descricao, content };
      const erros = derivarErros({ responses: { '400': resposta } });

      if (caso.esperado === 'pendente') {
        assert.equal(erros.length, 0, JSON.stringify({ caso, content }));
        continue;
      }
      assert.equal(erros.length, 1, JSON.stringify({ caso, content }));
      assert.equal(erros[0].status, 400);
      if (caso.esperado === 'codigos-nulos') assert.equal(erros[0].codigos, null);
      else assert.deepEqual(erros[0].codigos, ['nao_autenticado']);
    }
  }

  // No contrato real, os três 404 que dizem "Corpo vazio" derivam mesmo
  // declarando `content`, e os 404 de recurso continuam pendentes.
  const operacoes = new Map(listarOperacoes(lerContrato())
    .map((item) => [item.operationId, item.operacao]));
  for (const operationId of ['enviarNewsletter', 'previsualizarNewsletter', 'reverterCoordenadas']) {
    assert.ok(derivarErros(operacoes.get(operationId)).some((erro) => erro.status === 404),
      operationId);
  }
  assert.equal(derivarErros(operacoes.get('obterPescariaPorToken'))
    .some((erro) => erro.status === 404), false);
});

test('3XX nunca entra em erros derivados', () => {
  const erros = derivarErros({
    responses: {
      '302': { description: 'Redireciona para o app.', content: { 'text/html': {} } },
      '429': { description: 'Corpo vazio.' }
    }
  });
  assert.deepEqual(erros.map((erro) => erro.status), [429]);
});

test('auditar lista as pendências de erros da fixture', () => {
  // O `400` de cada operação com corpo e sem `codigo` na `description` é
  // pendência que o override precisa cobrir; `auditar` só a lista.
  assert.deepEqual(auditar(CONTRATO).pendencias, [
    { operationId: 'excluirConta', status: 400 },
    { operationId: 'obterCondicoes', status: 400 },
    { operationId: 'criarPescaria', status: 400 },
    { operationId: 'enviarMidia', status: 400 }
  ]);
  assert.equal(auditar(CONTRATO).derivados, 6);
});
