// Testes do gerador de referência: o contrato é a fonte do que é mecânico
// (método, rota, acesso, limites, cache, erros) e o override cobre o resto.
// Os casos de contrato real leem docs/api/openapi.json da árvore.
import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import {
  DOMINIOS,
  ErroDeGeracao,
  auditar,
  derivarConsentimento,
  derivarErros,
  gerarModelo,
  lerContrato,
  listarOperacoes,
  slugDoDominio
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
  ['excluirLocal', { arquivo: 'excluirLocal.md', frontmatter: { operacao: 'excluirLocal', proposta: 'Exclui o local.', acesso: 'dono', verificar: 'Veja o `204`.', caminho: { id: '00000000-0000-4000-8000-000000000001' } }, corpo: '' }],
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
    erro.problemas);
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
    assert.ok(erro.problemas.some((problema) => problema.includes('Inventada')), erro.problemas);
    return true;
  });
});

test('domínio da ordem canônica sem tag no contrato falha', () => {
  const contrato = { ...CONTRATO, tags: CONTRATO.tags.filter((tag) => tag.name !== 'Conta') };
  assert.throws(() => gerarModelo(contrato, OVERRIDES), (erro) => {
    assert.ok(erro instanceof ErroDeGeracao);
    assert.ok(erro.problemas.some((problema) => problema.includes('Conta')), erro.problemas);
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
    assert.ok(erro.problemas.some((problema) => problema.includes('naoExiste')), erro.problemas);
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
