import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

import {
  OWNER,
  PROJECT_ID,
  assertProjectToken,
  buildDraftBody,
  buildFieldListArgs,
  buildItemAddArgs,
  buildItemCreateArgs,
  buildItemListArgs,
  buildIterationEditArgs,
  buildSingleSelectEditArgs,
  findDuplicateTitles,
  indexProjectFields,
  mapEventToStatus,
  parseSeed
} from './github-project-sync.mjs';

const itemCompleto = {
  title: 'X',
  area: 'Web',
  evidencia: 'registrar.astro:33',
  impacto: 'Anônimo desiste de registrar',
  aceite: 'anônimo consegue digitar sem redirecionar',
  priority: 'P0',
  size: 'S',
  iteration: 'Iteration 1'
};

test('issues abertas entram como Backlog', () => {
  assert.equal(mapEventToStatus('issues', { action: 'opened' }), 'Backlog');
});

test('PR aberto entra como In review', () => {
  assert.equal(mapEventToStatus('pull_request', { action: 'opened' }), 'In review');
});

test('PR fechado e mergeado vira Done', () => {
  assert.equal(mapEventToStatus('pull_request', { action: 'closed', merged: true }), 'Done');
});

test('PR fechado sem merge não muda o Status', () => {
  assert.equal(mapEventToStatus('pull_request', { action: 'closed', merged: false }), null);
});

test('findDuplicateTitles devolve só os títulos que já existem', () => {
  assert.deepEqual(findDuplicateTitles(['A', 'B'], ['B', 'C']), ['B']);
});

test('semeadura ignora duplicados mesmo com diferença de caixa', async () => {
  const { selecionarItensNovos } = await import('./github-project-sync.mjs');

  assert.deepEqual(
    selecionarItensNovos(
      [{ title: 'Fonte e horário confiáveis' }, { title: 'Outro achado' }],
      ['fonte e horário confiáveis']
    ),
    [{ title: 'Outro achado' }]
  );
});

test('plano de seed rejeita iteração sem ID configurado', async () => {
  const { exigirIteracao } = await import('./github-project-sync.mjs');

  assert.throws(() => exigirIteracao('Iteration inexistente'), /Iteration inexistente/);
});

test('parseSeed aceita um item completo', () => {
  const itens = parseSeed(JSON.stringify([itemCompleto]));

  assert.equal(itens.length, 1);
  assert.equal(itens[0].title, 'X');
});

test('parseSeed rejeita item sem campo obrigatório', () => {
  assert.throws(() => parseSeed('[{"title":"X"}]'), /evidencia/);
});

test('buildDraftBody inclui as seções e o critério de aceite', () => {
  const corpo = buildDraftBody(itemCompleto);

  assert.match(corpo, /## Evidência/);
  assert.match(corpo, /## Impacto/);
  assert.match(corpo, /## Critério de aceite/);
  assert.ok(corpo.includes(itemCompleto.aceite));
});

test('assertProjectToken exige o token do quadro', () => {
  const ghTokenOriginal = process.env.GH_TOKEN;
  const githubTokenOriginal = process.env.GITHUB_TOKEN;

  try {
    delete process.env.GH_TOKEN;
    delete process.env.GITHUB_TOKEN;

    assert.throws(() => assertProjectToken(), /FISGA_PROJECT_TOKEN/);
  } finally {
    if (ghTokenOriginal === undefined) {
      delete process.env.GH_TOKEN;
    } else {
      process.env.GH_TOKEN = ghTokenOriginal;
    }

    if (githubTokenOriginal === undefined) {
      delete process.env.GITHUB_TOKEN;
    } else {
      process.env.GITHUB_TOKEN = githubTokenOriginal;
    }
  }
});

test('builders montam os argv de listagem, adição e criação', () => {
  assert.deepEqual(buildFieldListArgs(), [
    'project',
    'field-list',
    '6',
    '--owner',
    OWNER,
    '--format',
    'json',
    '--limit',
    '100'
  ]);

  assert.deepEqual(buildItemListArgs(), [
    'project',
    'item-list',
    '6',
    '--owner',
    OWNER,
    '--format',
    'json',
    '--limit',
    '100'
  ]);

  assert.deepEqual(buildItemAddArgs('https://github.com/AbsonDev/abson/issues/1'), [
    'project',
    'item-add',
    '6',
    '--owner',
    OWNER,
    '--url',
    'https://github.com/AbsonDev/abson/issues/1',
    '--format',
    'json'
  ]);

  assert.deepEqual(buildItemCreateArgs('Título', 'Corpo'), [
    'project',
    'item-create',
    '6',
    '--owner',
    OWNER,
    '--title',
    'Título',
    '--body',
    'Corpo',
    '--format',
    'json'
  ]);
});

test('edição dos quatro campos usa --id/--field-id/--project-id e valor tipado', () => {
  const itemId = 'PVTI_item_de_teste';
  const status = buildSingleSelectEditArgs(itemId, 'PVTSSF_status', 'PVTSSF_backlog');
  const priority = buildSingleSelectEditArgs(itemId, 'PVTSSF_priority', 'PVTSSF_p0');
  const size = buildSingleSelectEditArgs(itemId, 'PVTSSF_size', 'PVTSSF_s');
  const iteration = buildIterationEditArgs(itemId, 'PVTIF_iteration', '381c7c80');

  assert.deepEqual(status, [
    'project',
    'item-edit',
    '--id',
    itemId,
    '--field-id',
    'PVTSSF_status',
    '--project-id',
    PROJECT_ID,
    '--single-select-option-id',
    'PVTSSF_backlog'
  ]);

  assert.deepEqual(priority, [
    'project',
    'item-edit',
    '--id',
    itemId,
    '--field-id',
    'PVTSSF_priority',
    '--project-id',
    PROJECT_ID,
    '--single-select-option-id',
    'PVTSSF_p0'
  ]);

  assert.deepEqual(size, [
    'project',
    'item-edit',
    '--id',
    itemId,
    '--field-id',
    'PVTSSF_size',
    '--project-id',
    PROJECT_ID,
    '--single-select-option-id',
    'PVTSSF_s'
  ]);

  assert.deepEqual(iteration, [
    'project',
    'item-edit',
    '--id',
    itemId,
    '--field-id',
    'PVTIF_iteration',
    '--project-id',
    PROJECT_ID,
    '--iteration-id',
    '381c7c80'
  ]);

  for (const args of [status, priority, size, iteration]) {
    assert.ok(!args.includes('--field'), 'não pode usar --field com --id');
    assert.ok(!args.includes('--value'), 'não pode usar --value com --field-id');
    assert.ok(args.includes('--field-id'));
    assert.ok(args.includes('--project-id'));
  }
});

test('indexProjectFields mapeia campos, tipos e opções', () => {
  const indice = indexProjectFields({
    fields: [
      {
        id: 'PVTSSF_status',
        name: 'Status',
        type: 'ProjectV2SingleSelectField',
        options: [
          { id: 'PVTSSF_backlog', name: 'Backlog' },
          { id: 'PVTSSF_review', name: 'In review' }
        ]
      },
      {
        id: 'PVTIF_iteration',
        name: 'Iteration',
        type: 'ProjectV2IterationField'
      }
    ],
    totalCount: 2
  });

  assert.equal(indice.get('Status').id, 'PVTSSF_status');
  assert.equal(indice.get('Status').type, 'ProjectV2SingleSelectField');
  assert.equal(indice.get('Status').options.get('In review'), 'PVTSSF_review');
  assert.equal(indice.get('Iteration').id, 'PVTIF_iteration');
  assert.equal(indice.get('Iteration').options.size, 0);
});

test('CLI real rejeita --field com --id e aceita o caminho por IDs', (t) => {
  if (process.env.CI) {
    t.skip('smoke de rede pulado no CI');
    return;
  }

  const gh = spawnSync('gh', ['--version'], { encoding: 'utf8', timeout: 10000 });

  if (gh.error || gh.status !== 0) {
    t.skip('CLI gh indisponível neste ambiente');
    return;
  }

  const env = {
    ...process.env,
    GH_TOKEN: 'token-de-teste-sem-validade',
    GITHUB_TOKEN: '',
    NO_COLOR: '1'
  };

  // Controle negativo: prova que o erro de validação de flag seria detectado.
  const formaAntiga = spawnSync(
    'gh',
    [
      'project',
      'item-edit',
      '6',
      '--owner',
      OWNER,
      '--id',
      'PVTI_item_de_teste',
      '--field',
      'Status',
      '--value',
      'Backlog'
    ],
    { encoding: 'utf8', timeout: 30000, env }
  );
  const saidaAntiga = `${formaAntiga.stdout ?? ''}${formaAntiga.stderr ?? ''}`;

  assert.notEqual(formaAntiga.status, 0, 'a forma antiga deveria falhar');
  assert.match(saidaAntiga, /`--field` cannot be used with `--id`/);

  // Caminho novo: a CLI valida os flags e só falha na autenticação/escopo.
  const caminhoPorIds = spawnSync(
    'gh',
    buildSingleSelectEditArgs('PVTI_item_de_teste', 'PVTSSF_campo_de_teste', 'PVTSSF_opcao_de_teste'),
    { encoding: 'utf8', timeout: 30000, env }
  );
  const saida = `${caminhoPorIds.stdout ?? ''}${caminhoPorIds.stderr ?? ''}`;

  if (
    caminhoPorIds.error?.code === 'ETIMEDOUT' ||
    /dial tcp|no such host|network is unreachable|connection refused|proxyconnect/i.test(saida)
  ) {
    t.skip('sem rede para o teste de fumaça da CLI');
    return;
  }

  assert.notEqual(caminhoPorIds.status, 0, 'o token fictício deveria falhar');
  assert.doesNotMatch(
    saida,
    /cannot be used with|unknown flag|required flag|must be provided|invalid argument/i,
    `a CLI recusou os flags do caminho por IDs: ${saida}`
  );
  assert.match(saida, /401|unauthorized|bad credentials|authentication|scope/i);
});
