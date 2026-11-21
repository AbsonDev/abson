#!/usr/bin/env node
// Gera src/data/referencia.json: o contrato preenche tudo o que é mecânico
// (método, rota, acesso, limites, cache, erros) e o override cobre o que ele
// cala. As páginas .astro só renderizam o modelo.
import {
  existsSync,
  mkdirSync,
  readFileSync,
  realpathSync,
  writeFileSync
} from 'node:fs';
import { relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const RAIZ = new URL('../', import.meta.url);
const RAIZ_DO_FISGA = new URL('../../', import.meta.url);
const CAMINHO_DO_CONTRATO = new URL('../docs/api/openapi.json', RAIZ);
// Um Markdown por operação; aqui o diretório só dá o caminho relativo que a
// pendência mostra.
const DIRETORIO_DE_OVERRIDES = new URL('../docs/publico/operacoes/', RAIZ);
const DESTINO = new URL('src/data/referencia.json', RAIZ);

const METODOS = ['get', 'post', 'put', 'patch', 'delete'];
const CODIGO_NA_DESCRICAO = /`([a-z][a-z0-9]*(?:_[a-z0-9]+)+)`/g;
const TEXTO_DE_CONSENTIMENTO =
  'Exige consentimento vigente. Sem ele a resposta é `403` com o `codigo` ' +
  '`consentimento_pendente`.';

// A ordem é a de leitura do mapa de URL (docs/api/portal.md#mapa-de-url), não a
// do array `tags` do contrato, que vem da primeira aparição em `paths`.
export const DOMINIOS = Object.freeze([
  { slug: 'sistema', tag: 'Sistema' },
  { slug: 'catalogo', tag: 'Catálogo' },
  { slug: 'autenticacao', tag: 'Autenticação' },
  { slug: 'conta', tag: 'Conta' },
  { slug: 'pescarias', tag: 'Pescarias' },
  { slug: 'diario', tag: 'Diário' },
  { slug: 'condicoes', tag: 'Condições' },
  { slug: 'locais', tag: 'Locais' },
  { slug: 'pontos', tag: 'Pontos' },
  { slug: 'trilhas', tag: 'Trilhas' },
  { slug: 'midia', tag: 'Mídia' },
  { slug: 'especies', tag: 'Espécies' },
  { slug: 'newsletter', tag: 'Newsletter' }
]);

const AUTENTICACAO_TEXTO = Object.freeze({
  nenhuma: 'sem autenticação',
  sessao: 'cookie de sessão `fisga_auth`',
  admin: 'header `X-Admin-Token`'
});

// Forma legível de `security` para a linha "Autenticação: ..." da página.
function autenticacaoTexto(autenticacao) {
  return AUTENTICACAO_TEXTO[autenticacao] ?? null;
}

export class ErroDeGeracao extends Error {
  constructor(problemas) {
    super(`${problemas.length} pendência(s) na geração da referência.`);
    this.name = 'ErroDeGeracao';
    this.problemas = problemas;
  }
}

export function slugDoDominio(tag) {
  return tag.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

export function lerContrato(caminho = CAMINHO_DO_CONTRATO) {
  return JSON.parse(readFileSync(caminho, 'utf8'));
}

// Percorre `paths` na ordem de inserção e os métodos na ordem canônica, para a
// página de domínio listar as operações na ordem do contrato.
export function listarOperacoes(contrato) {
  const operacoes = [];
  for (const [rota, item] of Object.entries(contrato.paths ?? {})) {
    for (const metodo of METODOS) {
      const operacao = item?.[metodo];
      if (!operacao) continue;
      operacoes.push({
        operationId: operacao.operationId ?? null,
        metodo: metodo.toUpperCase(),
        rota,
        tag: operacao.tags?.[0] ?? null,
        operacao
      });
    }
  }
  return operacoes;
}

// `security` só admite três formas no contrato: vazio, um `cookieAuth` ou um
// `adminToken`. Qualquer outra é pendência, não palpite.
function formaDeAutenticacao(operacao) {
  const seguranca = operacao.security ?? [];
  if (seguranca.length === 0) return 'nenhuma';
  if (seguranca.length !== 1) return null;
  const nomes = Object.keys(seguranca[0]);
  if (nomes.length !== 1) return null;
  if (nomes[0] === 'cookieAuth') return 'sessao';
  if (nomes[0] === 'adminToken') return 'admin';
  return null;
}

// O consentimento não é allowlist própria: sai do contrato, das operações com
// `cookieAuth` cujo `403` cita `consentimento_pendente`.
export function derivarConsentimento(contrato) {
  const consentimento = new Set();
  for (const { operationId, operacao } of listarOperacoes(contrato)) {
    const temCookie = (operacao.security ?? []).some((esquema) => 'cookieAuth' in esquema);
    if (!temCookie) continue;
    const descricao = operacao.responses?.['403']?.description ?? '';
    if (descricao.includes('consentimento_pendente')) consentimento.add(operationId);
  }
  return consentimento;
}

function respostasDeErro(operacao) {
  return Object.entries(operacao.responses ?? {})
    .map(([status, resposta]) => [Number(status), resposta])
    .filter(([status]) => Number.isInteger(status) && status >= 400)
    .sort(([a], [b]) => a - b);
}

// Um par (operação, status) é derivável quando a `description` nomeia um
// `codigo` entre crases ou declara "Corpo vazio". O resto fica para o override.
export function derivarErros(operacao) {
  const erros = [];
  for (const [status, resposta] of respostasDeErro(operacao)) {
    const quando = (resposta?.description ?? '').trim();
    const codigos = [...quando.matchAll(CODIGO_NA_DESCRICAO)].map((achado) => achado[1]);
    if (codigos.length > 0) {
      erros.push({ status, codigos, quando, origem: 'contrato' });
    } else if (quando.includes('Corpo vazio')) {
      erros.push({ status, codigos: null, quando, origem: 'contrato' });
    }
  }
  return erros;
}

export function auditar(contrato) {
  let derivados = 0;
  const pendencias = [];
  for (const { operationId, operacao } of listarOperacoes(contrato)) {
    const statusDerivados = new Set(derivarErros(operacao).map((erro) => erro.status));
    derivados += statusDerivados.size;
    for (const [status] of respostasDeErro(operacao)) {
      if (!statusDerivados.has(status)) pendencias.push({ operationId, status });
    }
  }
  return { derivados, pendencias };
}

// `type` composto com `string` vira `string` (o valor chega como texto na
// query); o `format` entra entre parênteses quando existe. Ex.: `string (uuid)`.
function rotuloDoTipo(schema = {}) {
  const tipo = Array.isArray(schema.type)
    ? (schema.type.includes('string') ? 'string' : schema.type.find((valor) => valor !== 'null'))
    : schema.type;
  return [tipo, schema.format && `(${schema.format})`].filter(Boolean).join(' ');
}

function caminhoDoOverride(operationId) {
  const arquivo = new URL(`${operationId}.md`, DIRETORIO_DE_OVERRIDES);
  return relative(fileURLToPath(RAIZ_DO_FISGA), fileURLToPath(arquivo));
}

function validarTags(contrato) {
  const problemas = [];
  const tagsDoContrato = new Map();
  for (const tag of contrato.tags ?? []) {
    if (typeof tag?.name !== 'string' || tag.name.length === 0) {
      problemas.push('openapi.json: tags: tag do contrato sem nome');
      continue;
    }
    if (tagsDoContrato.has(tag.name)) {
      problemas.push(`openapi.json: tags: tag "${tag.name}" repetida no contrato`);
      continue;
    }
    tagsDoContrato.set(tag.name, tag);
  }

  const tagsDaOrdem = new Set(DOMINIOS.map((dominio) => dominio.tag));
  for (const tag of tagsDaOrdem) {
    if (!tagsDoContrato.has(tag)) {
      problemas.push(
        `openapi.json: tags: domínio "${tag}" sem tag no contrato ` +
          '(docs/api/portal.md#mapa-de-url)');
    }
  }
  for (const nome of tagsDoContrato.keys()) {
    if (!tagsDaOrdem.has(nome)) {
      problemas.push(
        `openapi.json: tags: tag "${nome}" fora da ordem canônica ` +
          '(docs/api/portal.md#mapa-de-url)');
    }
  }
  return { problemas, tagsDoContrato };
}

function derivarOperacao({ operationId, metodo, rota, tag, operacao }, dominio, consentimento) {
  const autenticacao = formaDeAutenticacao(operacao);
  if (autenticacao === null) {
    return {
      problemas: [
        `${operationId}: security: security não reconhecido ` +
          `(${JSON.stringify(operacao.security)})`
      ]
    };
  }

  const limites = operacao['x-rate-limit'];
  const temConsentimento = consentimento.has(operationId);
  return {
    problemas: [],
    operacao: {
      operationId,
      metodo,
      rota,
      tag,
      dominio: dominio.slug,
      summary: operacao.summary ?? '',
      descricao: operacao.description ?? '',
      autenticacao,
      autenticacaoTexto: autenticacaoTexto(autenticacao),
      consentimento: temConsentimento,
      consentimentoTexto: temConsentimento ? TEXTO_DE_CONSENTIMENTO : null,
      limites: limites
        ? {
            policy: limites.policy,
            requests: limites.requests,
            windowSeconds: limites.windowSeconds
          }
        : null,
      xCache: operacao['x-cache'] ?? null,
      parametros: (operacao.parameters ?? []).map((parametro) => ({
        nome: parametro.name,
        local: parametro.in,
        tipo: rotuloDoTipo(parametro.schema),
        obrigatorio: parametro.required === true
      })),
      erros: derivarErros(operacao)
    }
  };
}

export function gerarModelo(contrato, overrides) {
  const { problemas, tagsDoContrato } = validarTags(contrato);
  const consentimento = derivarConsentimento(contrato);
  const operacoes = {};
  const conhecidas = new Set();

  for (const item of listarOperacoes(contrato)) {
    const { operationId, metodo, rota, tag, operacao } = item;

    if (typeof operationId !== 'string' || operationId.length === 0) {
      problemas.push(`${metodo} ${rota}: operationId: operação sem operationId`);
      continue;
    }
    if (conhecidas.has(operationId)) {
      problemas.push(`${operationId}: operationId: repetido no contrato`);
      continue;
    }
    conhecidas.add(operationId);

    // Uma operação pertence a um domínio e só um: a URL da página é montada
    // por `dominio`, então duas tags deixariam a operação sem endereço.
    if ((operacao.tags ?? []).length !== 1) {
      problemas.push(`${operationId}: tags: a operação precisa de exatamente uma tag`);
      continue;
    }
    const dominio = DOMINIOS.find((candidato) => candidato.tag === tag);
    if (!dominio) {
      problemas.push(`${operationId}: tags: operação sem tag da ordem canônica`);
      continue;
    }

    if (!overrides.has(operationId)) {
      problemas.push(`${operationId}: override: falta ${caminhoDoOverride(operationId)}`);
    }

    const resultado = derivarOperacao(item, dominio, consentimento);
    problemas.push(...resultado.problemas);
    if (resultado.operacao) operacoes[operationId] = resultado.operacao;
  }

  for (const chave of overrides.keys()) {
    if (!conhecidas.has(chave)) {
      problemas.push(`${chave}: override: operação não existe no contrato`);
    }
  }

  const dominios = DOMINIOS.map(({ slug, tag }) => {
    const doContrato = tagsDoContrato.get(tag);
    const ids = Object.values(operacoes)
      .filter((operacao) => operacao.dominio === slug)
      .map((operacao) => operacao.operationId);
    return {
      slug,
      tag,
      titulo: doContrato?.name ?? tag,
      descricao: doContrato?.description ?? '',
      total: ids.length,
      operacoes: ids
    };
  });

  const servidores = (contrato.servers ?? []).map(({ url, description }) => ({
    url,
    descricao: description
  }));
  const info = {
    titulo: contrato.info?.title ?? '',
    descricao: contrato.info?.description ?? '',
    versao: contrato.info?.version ?? '',
    servidor: servidores[0]?.url ?? null,
    servidores
  };

  if (problemas.length > 0) throw new ErroDeGeracao(problemas);

  const totalDeOperacoes = Object.keys(operacoes).length;
  return {
    info,
    dominios,
    operacoes,
    contagem: {
      operacoes: totalDeOperacoes,
      dominios: dominios.length,
      // Home + índice de referência + um por domínio + um por operação.
      paginas: 2 + dominios.length + totalDeOperacoes
    }
  };
}

// Sem overrides lidos do disco, cada operação pendente sai na lista e nenhum
// JSON parcial é gravado: o arquivo só existe quando o modelo está completo.
function main() {
  let modelo;
  try {
    modelo = gerarModelo(lerContrato(), new Map());
  } catch (erro) {
    if (!(erro instanceof ErroDeGeracao)) throw erro;
    for (const problema of erro.problemas) console.error(problema);
    process.exitCode = 1;
    return;
  }

  mkdirSync(new URL('.', DESTINO), { recursive: true });
  writeFileSync(DESTINO, JSON.stringify(modelo, null, 2));
  console.log(
    `src/data/referencia.json gerado: ${modelo.contagem.operacoes} operações, ` +
      `${modelo.contagem.dominios} domínios, ${modelo.contagem.paginas} páginas.`);
}

// Importado pelos testes, o módulo só exporta as funções puras.
function chamadoDiretamente() {
  const alvo = process.argv[1];
  if (!alvo) return false;
  // `pathToFileURL` não resolve symlink (`/var` → `/private/var` no macOS):
  // comparar só a URL faria o CLI não rodar e a guarda passar em silêncio.
  return (
    pathToFileURL(alvo).href === import.meta.url ||
    (existsSync(alvo) &&
      realpathSync(alvo) === realpathSync(fileURLToPath(import.meta.url)))
  );
}

if (chamadoDiretamente()) {
  main();
}
