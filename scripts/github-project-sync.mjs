#!/usr/bin/env node
// Sincroniza issues, PRs e drafts do Fisga com o GitHub Project 6.
// Requer Node 24+, a CLI `gh` autenticada via FISGA_PROJECT_TOKEN (escopo `project`)
// e gh >= 2.40.0 para o caminho de edição por IDs; validado com gh 2.103.3.
// O token é lido do ambiente pela própria CLI; este script nunca o imprime.

import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export const OWNER = 'AbsonDev';
export const PROJECT_NUMBER = 6;
export const PROJECT_ID = 'PVT_kwHODKV4_c4BsOkA';

// IDs lidos do GraphQL do quadro em 18/11/2026.
export const ITERATION_IDS = {
  'Iteration 1': '381c7c80',
  'Iteration 2': '54cf5c95',
  'Iteration 3': 'd2c335bc'
};

const TIPO_SELECAO_UNICA = 'ProjectV2SingleSelectField';
const TIPO_ITERACAO = 'ProjectV2IterationField';
const STATUS_VALIDOS = ['Backlog', 'Ready', 'In progress', 'In review', 'Done'];
const PRIORITY_VALIDOS = ['P0', 'P1', 'P2'];
const SIZE_VALIDOS = ['XS', 'S', 'M', 'L', 'XL'];
const CAMPOS_OBRIGATORIOS = [
  'title',
  'area',
  'evidencia',
  'impacto',
  'aceite',
  'priority',
  'size',
  'iteration'
];

const USO = `Uso:
  node scripts/github-project-sync.mjs event-status
  node scripts/github-project-sync.mjs add-or-sync <url> <status>
  node scripts/github-project-sync.mjs seed <arquivo.json>`;

export function buildFieldListArgs() {
  return [
    'project',
    'field-list',
    String(PROJECT_NUMBER),
    '--owner',
    OWNER,
    '--format',
    'json',
    '--limit',
    '100'
  ];
}

export function buildItemListArgs() {
  return [
    'project',
    'item-list',
    String(PROJECT_NUMBER),
    '--owner',
    OWNER,
    '--format',
    'json',
    '--limit',
    '100'
  ];
}

export function buildItemAddArgs(url) {
  return [
    'project',
    'item-add',
    String(PROJECT_NUMBER),
    '--owner',
    OWNER,
    '--url',
    url,
    '--format',
    'json'
  ];
}

export function buildItemCreateArgs(title, body) {
  return [
    'project',
    'item-create',
    String(PROJECT_NUMBER),
    '--owner',
    OWNER,
    '--title',
    title,
    '--body',
    body,
    '--format',
    'json'
  ];
}

// A CLI não aceita `--field`/`--value` junto de `--id`; para script o caminho é
// sempre por IDs de item, campo, projeto e opção.
export function buildSingleSelectEditArgs(itemId, fieldId, optionId) {
  return [
    'project',
    'item-edit',
    '--id',
    itemId,
    '--field-id',
    fieldId,
    '--project-id',
    PROJECT_ID,
    '--single-select-option-id',
    optionId
  ];
}

export function buildIterationEditArgs(itemId, fieldId, iterationId) {
  return [
    'project',
    'item-edit',
    '--id',
    itemId,
    '--field-id',
    fieldId,
    '--project-id',
    PROJECT_ID,
    '--iteration-id',
    iterationId
  ];
}

/**
 * Converte a saída de `gh project field-list --format json` em um mapa
 * nome do campo → { id, type, options: Map(nome da opção → id) }.
 */
export function indexProjectFields(payload) {
  if (!Array.isArray(payload?.fields)) {
    throw new Error(
      'Resposta inesperada de "gh project field-list": esperado um objeto com a lista "fields".'
    );
  }

  const campos = new Map();

  for (const campo of payload.fields) {
    if (!campo?.id || !campo?.name) {
      continue;
    }

    const opcoes = new Map();

    for (const opcao of Array.isArray(campo.options) ? campo.options : []) {
      if (opcao?.id && opcao?.name) {
        opcoes.set(opcao.name, opcao.id);
      }
    }

    campos.set(campo.name, { id: campo.id, type: campo.type ?? '', options: opcoes });
  }

  return campos;
}

/**
 * Traduz o evento do GitHub no Status do quadro.
 * Devolve null quando o evento não deve mexer no Status.
 */
export function mapEventToStatus(eventName, payload = {}) {
  const action = payload?.action;
  const merged = payload?.pull_request?.merged ?? payload?.merged;

  if (eventName === 'issues' && action === 'opened') {
    return 'Backlog';
  }

  if (eventName === 'pull_request') {
    if (action === 'opened') {
      return 'In review';
    }

    if (action === 'closed') {
      return merged === true ? 'Done' : null;
    }
  }

  return null;
}

function chaveTitulo(titulo) {
  return String(titulo ?? '').trim().toLowerCase();
}

/**
 * Devolve os títulos que chegaram e já existem, sem repetir.
 * A comparação ignora espaços nas pontas e diferença de maiúsculas.
 */
export function findDuplicateTitles(existingTitles, incomingTitles) {
  const existentes = new Set(existingTitles.map(chaveTitulo));
  const duplicados = [];
  const vistos = new Set();

  for (const titulo of incomingTitles) {
    const chave = chaveTitulo(titulo);

    if (!chave || !existentes.has(chave) || vistos.has(chave)) {
      continue;
    }

    vistos.add(chave);
    duplicados.push(titulo);
  }

  return duplicados;
}

function validarItemSeed(item, indice) {
  const posicao = `item ${indice + 1}`;

  if (item === null || typeof item !== 'object' || Array.isArray(item)) {
    throw new Error(`Seed inválido no ${posicao}: esperado um objeto JSON.`);
  }

  const titulo = typeof item.title === 'string' ? item.title.trim() : '';
  const rotulo = titulo ? `${posicao} ("${titulo}")` : posicao;
  const faltando = CAMPOS_OBRIGATORIOS.filter(
    (campo) => typeof item[campo] !== 'string' || item[campo].trim() === ''
  );

  if (faltando.length > 0) {
    throw new Error(
      `Seed inválido no ${rotulo}: campos obrigatórios ausentes ou vazios: ${faltando.join(', ')}.`
    );
  }

  const regras = [
    ['priority', PRIORITY_VALIDOS],
    ['size', SIZE_VALIDOS],
    ['iteration', Object.keys(ITERATION_IDS)]
  ];

  for (const [campo, aceitos] of regras) {
    const valor = item[campo].trim();

    if (!aceitos.includes(valor)) {
      throw new Error(
        `Seed inválido no ${rotulo}: ${campo} "${valor}" não é aceito; use ${aceitos.join(', ')}.`
      );
    }
  }

  return {
    title: titulo,
    area: item.area.trim(),
    evidencia: item.evidencia.trim(),
    impacto: item.impacto.trim(),
    aceite: item.aceite.trim(),
    priority: item.priority.trim(),
    size: item.size.trim(),
    iteration: item.iteration.trim()
  };
}

/**
 * Valida o JSON do seed e devolve os itens normalizados.
 * Lança erro em pt-BR apontando o item e os campos com problema.
 */
export function parseSeed(raw) {
  let dados;

  try {
    dados = JSON.parse(raw);
  } catch (erro) {
    throw new Error(`Seed inválido: JSON malformado (${erro.message}).`);
  }

  if (!Array.isArray(dados)) {
    throw new Error('Seed inválido: esperado um array JSON de itens.');
  }

  const itens = dados.map((item, indice) => validarItemSeed(item, indice));
  const titulos = new Set();

  for (const item of itens) {
    const chave = item.title.toLowerCase();

    if (titulos.has(chave)) {
      throw new Error(`Seed inválido: título repetido "${item.title}".`);
    }

    titulos.add(chave);
  }

  return itens;
}

/**
 * Monta o corpo do draft com o que foi visto, o impacto e o critério de aceite.
 */
export function buildDraftBody(item) {
  return [
    `**Área:** ${item.area}`,
    '',
    '## Evidência',
    item.evidencia,
    '',
    '## Impacto',
    item.impacto,
    '',
    '## Critério de aceite',
    item.aceite
  ].join('\n');
}

/**
 * Garante que o token do quadro está no ambiente.
 * A mensagem cita o nome do secret para o erro ser acionável no log da Action.
 */
export function assertProjectToken() {
  const token = process.env.GH_TOKEN?.trim() || process.env.GITHUB_TOKEN?.trim();

  if (!token) {
    throw new Error(
      'Token do quadro ausente: defina o secret FISGA_PROJECT_TOKEN como GH_TOKEN (ou GITHUB_TOKEN) no ambiente, com escopo "project".'
    );
  }
}

function executarGh(argumentos) {
  try {
    return execFileSync('gh', argumentos, {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      maxBuffer: 32 * 1024 * 1024
    });
  } catch (erro) {
    const stderr = String(erro.stderr ?? '').trim();
    const detalhe = stderr || erro.message;
    const comando = ['gh', ...argumentos.slice(0, 2)].join(' ');

    throw new Error(`Falha ao executar "${comando}": ${detalhe}`);
  }
}

function executarGhJson(argumentos) {
  const saida = executarGh(argumentos);

  try {
    return JSON.parse(saida);
  } catch {
    const comando = ['gh', ...argumentos.slice(0, 2)].join(' ');

    throw new Error(`Saída JSON inválida de "${comando}".`);
  }
}

function validarStatus(status) {
  if (!STATUS_VALIDOS.includes(status)) {
    throw new Error(`Status inválido: "${status}". Use um de: ${STATUS_VALIDOS.join(', ')}.`);
  }
}

function carregarCamposDoQuadro() {
  return indexProjectFields(executarGhJson(buildFieldListArgs()));
}

function exigirCampo(campos, nome, tipoEsperado) {
  const campo = campos.get(nome);

  if (!campo) {
    throw new Error(
      `Campo "${nome}" não encontrado no quadro ${PROJECT_NUMBER}; confira os campos Status, Priority, Size e Iteration.`
    );
  }

  if (campo.type !== tipoEsperado) {
    throw new Error(
      `Campo "${nome}" tem tipo "${campo.type || 'desconhecido'}", esperado "${tipoEsperado}".`
    );
  }

  return campo;
}

function exigirOpcao(campo, nomeOpcao, nomeCampo) {
  const id = campo.options.get(nomeOpcao);

  if (!id) {
    const disponiveis = [...campo.options.keys()].join(', ') || 'nenhuma';

    throw new Error(
      `Opção "${nomeOpcao}" não existe no campo "${nomeCampo}" do quadro ${PROJECT_NUMBER}; disponíveis: ${disponiveis}.`
    );
  }

  return id;
}

function adicionarOuSincronizar(url, status) {
  assertProjectToken();
  validarStatus(status);

  // Resolve campo/opção antes de adicionar: nenhum item entra sem Status.
  const statusField = exigirCampo(carregarCamposDoQuadro(), 'Status', TIPO_SELECAO_UNICA);
  const statusOptionId = exigirOpcao(statusField, status, 'Status');

  // addProjectV2ItemById é idempotente: se o item já existe no quadro,
  // a CLI devolve o id existente em vez de criar outro.
  const adicionado = executarGhJson(buildItemAddArgs(url));
  const itemId = adicionado.id;

  if (!itemId) {
    throw new Error('A CLI gh não retornou o id do item adicionado ao quadro.');
  }

  executarGh(buildSingleSelectEditArgs(itemId, statusField.id, statusOptionId));

  return { id: itemId, status, url };
}

function listarTitulosExistentes() {
  const listagem = executarGhJson(buildItemListArgs());
  const itens = Array.isArray(listagem.items) ? listagem.items : [];
  const total = Number(listagem.totalCount ?? itens.length);

  if (total > itens.length) {
    console.error(
      `Aviso: o quadro tem ${total} itens, mas só ${itens.length} foram lidos; a checagem de duplicados pode estar incompleta.`
    );
  }

  return itens
    .map((item) => item?.content?.title ?? item?.title ?? '')
    .filter((titulo) => titulo !== '');
}

function planejarItensDoSeed(itens, campos) {
  const prioridade = exigirCampo(campos, 'Priority', TIPO_SELECAO_UNICA);
  const tamanho = exigirCampo(campos, 'Size', TIPO_SELECAO_UNICA);
  const iteracao = exigirCampo(campos, 'Iteration', TIPO_ITERACAO);

  // Resolve todos os IDs antes do primeiro item-create: erro de configuração
  // do quadro falha o seed sem deixar drafts incompletos.
  return itens.map((item) => ({
    item,
    priority: {
      fieldId: prioridade.id,
      optionId: exigirOpcao(prioridade, item.priority, 'Priority')
    },
    size: {
      fieldId: tamanho.id,
      optionId: exigirOpcao(tamanho, item.size, 'Size')
    },
    iteration: {
      fieldId: iteracao.id,
      iterationId: ITERATION_IDS[item.iteration]
    }
  }));
}

function criarItemDoSeed(plano) {
  const { item, priority, size, iteration } = plano;
  const criado = executarGhJson(buildItemCreateArgs(item.title, buildDraftBody(item)));
  const itemId = criado.id;

  if (!itemId) {
    throw new Error(`A CLI gh não retornou o id do draft "${item.title}".`);
  }

  const edicoes = [
    ['Priority', buildSingleSelectEditArgs(itemId, priority.fieldId, priority.optionId)],
    ['Size', buildSingleSelectEditArgs(itemId, size.fieldId, size.optionId)],
    ['Iteration', buildIterationEditArgs(itemId, iteration.fieldId, iteration.iterationId)]
  ];

  for (const [campo, argumentos] of edicoes) {
    try {
      executarGh(argumentos);
    } catch (erro) {
      throw new Error(
        `Draft "${item.title}" criado, mas o campo ${campo} não foi preenchido: ${erro.message}. ` +
          'Ajuste manualmente ou remova o draft e rode o seed de novo.'
      );
    }
  }

  return itemId;
}

function semearQuadro(caminho) {
  assertProjectToken();

  let bruto;

  try {
    bruto = readFileSync(caminho, 'utf8');
  } catch (erro) {
    throw new Error(`Não foi possível ler o seed "${caminho}": ${erro.message}`);
  }

  const itens = parseSeed(bruto);
  const duplicados = findDuplicateTitles(
    listarTitulosExistentes(),
    itens.map((item) => item.title)
  );
  const duplicadosSet = new Set(duplicados);
  const novos = itens.filter((item) => !duplicadosSet.has(item.title));
  const planos = novos.length > 0 ? planejarItensDoSeed(novos, carregarCamposDoQuadro()) : [];
  const criados = planos.map((plano) => ({
    id: criarItemDoSeed(plano),
    title: plano.item.title
  }));

  return {
    created: criados.length,
    skipped: duplicados.length,
    skippedTitles: duplicados,
    items: criados
  };
}

function inferirEvento(payload) {
  if (payload?.pull_request) {
    return 'pull_request';
  }

  if (payload?.issue) {
    return 'issues';
  }

  return '';
}

function statusDoEvento() {
  const caminho = process.env.GITHUB_EVENT_PATH;

  if (!caminho) {
    throw new Error(
      'GITHUB_EVENT_PATH não definido: event-status precisa do caminho do payload do evento.'
    );
  }

  let conteudo;

  try {
    conteudo = readFileSync(caminho, 'utf8');
  } catch (erro) {
    throw new Error(`Não foi possível ler o payload do evento em "${caminho}": ${erro.message}`);
  }

  let payload;

  try {
    payload = JSON.parse(conteudo);
  } catch (erro) {
    throw new Error(`Payload do evento inválido em "${caminho}": ${erro.message}`);
  }

  const eventName = process.env.GITHUB_EVENT_NAME?.trim() || inferirEvento(payload);

  return mapEventToStatus(eventName, payload);
}

function executarCli(argumentos) {
  const [comando, ...resto] = argumentos;

  if (!comando || comando === '--help' || comando === '-h') {
    console.log(USO);
    return;
  }

  if (comando === 'event-status') {
    const status = statusDoEvento();

    if (status) {
      console.log(status);
    }

    return;
  }

  if (comando === 'add-or-sync') {
    const [url, status] = resto;

    if (!url || !status) {
      throw new Error(`add-or-sync exige <url> e <status>.\n${USO}`);
    }

    console.log(JSON.stringify(adicionarOuSincronizar(url, status), null, 2));
    return;
  }

  if (comando === 'seed') {
    const [arquivo] = resto;

    if (!arquivo) {
      throw new Error(`seed exige o caminho de um arquivo JSON.\n${USO}`);
    }

    console.log(JSON.stringify(semearQuadro(arquivo), null, 2));
    return;
  }

  throw new Error(`Comando desconhecido: "${comando}".\n${USO}`);
}

const caminhoDoModulo = process.argv[1] ? pathToFileURL(process.argv[1]).href : '';

if (import.meta.url === caminhoDoModulo) {
  try {
    executarCli(process.argv.slice(2));
  } catch (erro) {
    console.error(`Erro: ${erro.message}`);
    process.exitCode = 1;
  }
}
