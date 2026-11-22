#!/usr/bin/env node
// Gera src/data/referencia.json: o contrato preenche tudo o que é mecânico
// (método, rota, acesso, limites, cache, erros) e o override cobre o que ele
// cala. As páginas .astro só renderizam o modelo.
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  realpathSync,
  writeFileSync
} from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { marked } from 'marked';
import { parse as parseYaml } from 'yaml';

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
const UUID_DE_EXEMPLO =
  /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

// Chaves que o override pode declarar. Tudo o que o contrato já sabe fica de
// fora: repetir seria uma segunda fonte para o mesmo dado.
const CHAVES_DO_OVERRIDE = Object.freeze([
  'operacao', 'proposta', 'acesso', 'caminho', 'consulta', 'corpo', 'arquivos',
  'verificar', 'relacionados', 'erros'
]);

// `acesso` é decisão editorial; o texto é fixo para a página não reescrever a
// mesma frase em 54 lugares.
const ACESSO_TEXTO = Object.freeze({
  publico: 'qualquer pessoa, sem autenticação',
  capacidade: 'quem tem o link ou o token de compartilhamento',
  sessao: 'qualquer pessoa com sessão iniciada',
  dono: 'só o dono do recurso',
  admin: 'só o operador com o token administrativo'
});

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

// Segue `$ref` de `#/components/schemas/<Nome>` até o schema final. O `Set` de
// nomes é a guarda de ciclo: contrato futuro que volte ao mesmo nome para em
// `{}` em vez de girar. Hoje os 77 schemas do contrato não se alcançam. Serve
// tanto à tabela e ao esqueleto quanto à validação do `requestBody`.
export function resolverRef(schema, contrato) {
  let atual = schema;
  const vistos = new Set();
  while (atual && typeof atual === 'object' && atual.$ref) {
    const nome = String(atual.$ref).replace(/^#\/components\/schemas\//, '');
    if (vistos.has(nome)) return {};
    vistos.add(nome);
    atual = contrato?.components?.schemas?.[nome];
  }
  return atual ?? {};
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

// O diretório de overrides é a única camada que vê todos os arquivos, então é
// aqui que nome fora de `<operacao>.md` e `operacao` repetida viram problema.
export function lerOverrides(diretorio = DIRETORIO_DE_OVERRIDES) {
  // Aceita `URL` (o padrão) e também caminho em texto, como `lerContrato`.
  const base = diretorio instanceof URL ? fileURLToPath(diretorio) : String(diretorio);
  if (!existsSync(base)) return [];

  const entradas = [];
  const problemas = [];
  const vistas = new Map();
  const arquivos = readdirSync(base, { withFileTypes: true })
    .filter((item) => item.isFile() && item.name.endsWith('.md'))
    .map((item) => item.name)
    .sort();

  for (const arquivo of arquivos) {
    const texto = readFileSync(join(base, arquivo), 'utf8');
    const partes = lerFrontmatter(texto);
    if (partes.erro) {
      problemas.push(`${arquivo}: frontmatter: ${partes.erro}`);
      continue;
    }

    const esperado = arquivo.replace(/\.md$/, '');
    const operacao = partes.dados.operacao;
    if (typeof operacao !== 'string' || operacao.trim().length === 0) {
      problemas.push(`${arquivo}: operacao: obrigatório e não vazio`);
      continue;
    }
    if (operacao !== esperado) {
      problemas.push(`${arquivo}: operacao: o nome do arquivo precisa ser ` +
        `<operacao>.md (${esperado}.md declara ${JSON.stringify(operacao)})`);
      continue;
    }
    // Defensivo: com o nome do arquivo amarrado à operação, dois arquivos só
    // colidiriam num sistema de arquivos que diferencie caixa.
    if (vistas.has(operacao)) {
      problemas.push(`${arquivo}: operacao: repetida em ${vistas.get(operacao)}`);
      continue;
    }
    vistas.set(operacao, arquivo);
    entradas.push({
      arquivo,
      operacao,
      frontmatter: partes.dados,
      corpo: partes.corpo
    });
  }

  if (problemas.length > 0) throw new ErroDeGeracao(problemas);
  return entradas;
}

// O frontmatter são as duas primeiras linhas `---`; o resto do arquivo é corpo.
// O BOM do editor não pode esconder a primeira linha.
function lerFrontmatter(texto) {
  const linhas = texto.replace(/^\uFEFF/, '').split('\n');
  if (linhas[0]?.trim() !== '---') {
    return { erro: 'o arquivo precisa abrir com bloco entre `---`' };
  }
  const fim = linhas.findIndex((linha, indice) => indice > 0 && linha.trim() === '---');
  if (fim === -1) return { erro: 'o bloco entre `---` não foi fechado' };

  let dados;
  try {
    dados = parseYaml(linhas.slice(1, fim).join('\n')) ?? {};
  } catch (erro) {
    return { erro: `YAML inválido (${erro.message})` };
  }
  if (typeof dados !== 'object' || Array.isArray(dados)) {
    return { erro: 'o frontmatter precisa ser um mapa YAML' };
  }
  return { dados, corpo: linhas.slice(fim + 1).join('\n').trim() };
}

// Um heading no corpo brigaria com as seções fixas da página; linha `#` dentro
// de bloco de código é conteúdo. `marked` aceita até três espaços de recuo e
// renderiza `<h1>` também para HTML cru, então as duas formas entram na regra.
function temHeading(corpo) {
  let dentroDeBloco = false;
  for (const linha of corpo.split('\n')) {
    if (linha.trimStart().startsWith('```')) {
      dentroDeBloco = !dentroDeBloco;
      continue;
    }
    const inicio = linha.trimStart();
    if (!dentroDeBloco && (inicio.startsWith('#') || /^<h[1-6][\s>]/i.test(inicio))) {
      return true;
    }
  }
  return false;
}

function parametrosDe(operacao, local) {
  return (operacao.operacao.parameters ?? []).filter((parametro) => parametro.in === local);
}

function listaDeCodigos(valor) {
  // R27: `codigos: []` explícito vira `null` no modelo — a página renderiza `—`.
  if (Array.isArray(valor)) return valor.length > 0 ? valor : null;
  if (typeof valor === 'string' && valor.trim().length > 0) return [valor];
  return null;
}

// `<campo>: <motivo>`; quem chama prefixa com o nome do arquivo.
function problemasDeCaminhoEConsulta(entrada, operacao) {
  const problemas = [];
  const caminhos = parametrosDe(operacao, 'path');
  const consultas = parametrosDe(operacao, 'query');
  const frontmatter = entrada.frontmatter;

  // Um problema de forma por chave: sem o curto-circuito, `null` também acusava
  // "proibido"/"obrigatório" e o autor lia a mesma causa duas vezes.
  const invalidos = new Set();
  for (const [campo, parametros, local] of [
    ['caminho', caminhos, 'path'],
    ['consulta', consultas, 'query']
  ]) {
    const valor = frontmatter[campo];
    if (valor === undefined) continue;
    if (valor === null || typeof valor !== 'object' || Array.isArray(valor)) {
      problemas.push(`${campo}: precisa ser objeto de valores concretos`);
      invalidos.add(campo);
      continue;
    }
    const nomes = new Set(parametros.map((parametro) => parametro.name));
    // Rota sem parâmetro nesse local: o `{campo}` inteiro já é recusado como
    // proibido abaixo, então uma mensagem por chave só repetiria a causa.
    for (const chave of parametros.length === 0 ? [] : Object.keys(valor)) {
      if (!nomes.has(chave)) {
        problemas.push(`${campo}: chave fora de parameters: "${chave}" não é ${local}`);
      }
      // `null` e string vazia viram `/null` e `?q=` na URL do exemplo.
      const item = valor[chave];
      if (item === null || (typeof item === 'string' && item.trim().length === 0)) {
        problemas.push(`${campo}: ${chave}: precisa de valor concreto (null ou vazio não ` +
          'forma URL)');
      }
    }
  }

  const caminho = frontmatter.caminho;
  if (!invalidos.has('caminho')) {
    // Toda chave `caminho` exigida pela rota precisa de valor: sem ele a rota do
    // exemplo fica com `{param}` cru.
    if (caminhos.length > 0 && caminho === undefined) {
      problemas.push('caminho: obrigatório porque a rota tem parâmetro de caminho');
    }
    if (caminho && typeof caminho === 'object' && !Array.isArray(caminho)) {
      for (const parametro of caminhos) {
        if (parametro.required === true && !(parametro.name in caminho)) {
          problemas.push(`caminho: falta o parâmetro ${parametro.name}`);
        }
      }
    }
    if (caminhos.length === 0 && caminho !== undefined) {
      problemas.push('caminho: proibido porque a rota não tem parâmetro de caminho');
    }
  }

  // UUID de exemplo precisa ser versão 4: é o que o contrato publica nos 8
  // parâmetros `format: uuid`. Valor nulo/vazio já foi acusado acima.
  for (const [campo, parametros] of [['caminho', caminhos], ['consulta', consultas]]) {
    const valores = frontmatter[campo];
    if (!valores || typeof valores !== 'object' || Array.isArray(valores)) continue;
    for (const parametro of parametros) {
      const valor = valores[parametro.name];
      if (valor === undefined || valor === null) continue;
      if (typeof valor === 'string' && valor.trim().length === 0) continue;
      if (parametro.schema?.format === 'uuid' && !UUID_DE_EXEMPLO.test(String(valor))) {
        problemas.push(`${campo}: ${parametro.name}: precisa ser um UUID versão 4 ` +
          `(${JSON.stringify(valor)} não é)`);
      }
    }
  }

  const consulta = frontmatter.consulta;
  if (!invalidos.has('consulta')) {
    if (consultas.length === 0 && consulta !== undefined) {
      problemas.push('consulta: proibida porque a rota não tem parâmetro de query');
    } else if (consultas.length > 0) {
      const vazia = consulta === undefined ||
        (typeof consulta === 'object' && !Array.isArray(consulta) &&
          Object.keys(consulta).length === 0);
      if (vazia) {
        problemas.push('consulta: obrigatória porque a rota declara parâmetro de query');
      }
    }
  }

  return problemas;
}

function problemasDeCorpoEArquivos(entrada, operacao, contrato) {
  const problemas = [];
  const conteudo = operacao.operacao.requestBody?.content ?? {};
  const temJson = 'application/json' in conteudo;
  const temMultipart = 'multipart/form-data' in conteudo;
  const corpo = entrada.frontmatter.corpo;
  const arquivos = entrada.frontmatter.arquivos;

  if (corpo !== undefined) {
    if (!temJson) {
      problemas.push('corpo: proibido porque a operação não declara requestBody JSON');
    } else if (typeof corpo !== 'string') {
      problemas.push('corpo: precisa ser string JSON');
    } else {
      let valor;
      try {
        valor = JSON.parse(corpo);
      } catch (erro) {
        problemas.push(`corpo: JSON inválido (${erro.message})`);
        valor = undefined;
      }
      if (valor !== undefined) {
        if (valor === null || typeof valor !== 'object' || Array.isArray(valor)) {
          problemas.push('corpo: o corpo precisa ser objeto JSON; lista ou null não ' +
            'é um exemplo válido');
        } else {
          const schema = resolverRef(conteudo['application/json'].schema, contrato);
          const faltando = (schema.required ?? []).filter((chave) => !(chave in valor));
          if (faltando.length > 0) {
            problemas.push(`corpo: faltam propriedades obrigatórias: ${faltando.join(', ')}`);
          }
        }
      }
    }
  } else if (temJson) {
    problemas.push('corpo: obrigatório porque a operação declara requestBody JSON');
  }

  if (arquivos !== undefined) {
    if (!temMultipart) {
      problemas.push('arquivos: proibido porque a operação não declara multipart/form-data');
    } else if (arquivos === null || typeof arquivos !== 'object' || Array.isArray(arquivos) ||
      Object.keys(arquivos).length === 0) {
      problemas.push('arquivos: precisa ser objeto não vazio de campo para nome de arquivo');
    } else {
      const schema = resolverRef(conteudo['multipart/form-data'].schema, contrato);
      const campos = new Set(Object.keys(schema.properties ?? {}));
      for (const [campo, nome] of Object.entries(arquivos)) {
        if (!campos.has(campo)) problemas.push(`arquivos: campo fora do schema: ${campo}`);
        if (typeof nome !== 'string' || nome.trim().length === 0) {
          problemas.push(`arquivos: ${campo}: precisa do nome do arquivo do exemplo`);
        }
      }
      for (const exigido of schema.required ?? []) {
        if (!(exigido in arquivos)) problemas.push(`arquivos: falta o campo ${exigido}`);
      }
    }
  } else if (temMultipart) {
    problemas.push('arquivos: obrigatório porque a operação declara multipart/form-data');
  }

  return problemas;
}

function problemasDeErros(entrada, operacao) {
  const problemas = [];
  const declarados = new Map(respostasDeErro(operacao.operacao));
  const derivados = new Set(derivarErros(operacao.operacao).map((erro) => erro.status));
  // `erros: null` é erro de forma; ausente é só "sem linha de override" e cai
  // na checagem de pendência no fim.
  const erros = entrada.frontmatter.erros;
  if (erros !== undefined && !Array.isArray(erros)) {
    return ['erros: precisa ser lista de objetos'];
  }
  const linhas = erros ?? [];

  const vistos = new Set();
  for (const erro of linhas) {
    if (erro === null || typeof erro !== 'object' || Array.isArray(erro)) {
      problemas.push('erros: cada linha precisa ser objeto com status, codigos e quando');
      continue;
    }
    const { status } = erro;
    if (!Number.isInteger(status)) {
      problemas.push(`erros: status precisa ser inteiro (${JSON.stringify(status)})`);
      continue;
    }
    if (vistos.has(status)) problemas.push(`erros: status repetido: ${status}`);
    vistos.add(status);

    if (!declarados.has(status)) {
      problemas.push(`erros: status não declarado no contrato: ${status}`);
    } else if (derivados.has(status)) {
      problemas.push(`erros: status ${status}: linha derivável do contrato — ` +
        'o override só escreve o que a description não nomeia');
    }

    // R27: `codigos` presente é a decisão do autor. `[]` declara "este status
    // não nomeia código" (é o caso do 400 de `entrarComSenha`/`esquecerSenha`);
    // ausente, só `codigo` vale; vazio e sem nada é omissão.
    const codigos = erro.codigos;
    const codigo = erro.codigo;
    const codigoValido = typeof codigo === 'string' && codigo.trim().length > 0;
    if (codigos !== undefined && !Array.isArray(codigos)) {
      problemas.push(`erros: status ${status}: codigos precisa ser lista de strings`);
    } else if (Array.isArray(codigos)) {
      if (codigos.length === 0 && codigoValido) {
        problemas.push(`erros: status ${status}: \`codigos: []\` contradiz ` +
          `\`codigo: ${codigo}\` — escolha um`);
      } else if (codigos.length === 0 && 'codigo' in erro && !codigoValido) {
        problemas.push(`erros: status ${status}: codigo precisa ser string não vazia`);
      } else if (codigos.some((nome) =>
        typeof nome !== 'string' || nome.trim().length === 0)) {
        problemas.push(`erros: status ${status}: codigos precisa ser lista não vazia ` +
          'de strings (ou o atalho `codigo` com uma string; `[]` declara status sem código)');
      }
    } else if (!codigoValido) {
      problemas.push(`erros: status ${status}: codigos precisa ser lista não vazia ` +
        'de strings (ou o atalho `codigo` com uma string; `[]` declara status sem código)');
    }
    if (typeof erro.quando !== 'string' || erro.quando.trim().length === 0) {
      problemas.push(`erros: status ${status}: quando precisa dizer quando acontece`);
    }
  }

  // Todo par pendente precisa de linha: sem ela a página mostraria um status
  // sem explicação.
  for (const status of declarados.keys()) {
    if (!derivados.has(status) && !vistos.has(status)) {
      problemas.push(`erros: falta a linha do status ${status}`);
    }
  }
  return problemas;
}

// `operacao` é o item de `listarOperacoes` (o nó cru do contrato vive em
// `operacao.operacao`); `$ref` resolve em `contrato.components.schemas`.
export function validarOverride(entrada, operacao, contrato) {
  const frontmatter = entrada.frontmatter ?? {};
  if (typeof frontmatter !== 'object' || Array.isArray(frontmatter)) {
    return [`${entrada.arquivo}: frontmatter: precisa ser objeto`];
  }

  const problemas = [];
  const campo = (texto) => `${entrada.arquivo}: ${texto}`;

  for (const chave of Object.keys(frontmatter)) {
    if (!CHAVES_DO_OVERRIDE.includes(chave)) {
      problemas.push(campo(`frontmatter: chave desconhecida: ${chave}`));
    }
  }

  for (const chave of ['operacao', 'proposta', 'acesso', 'verificar']) {
    const valor = frontmatter[chave];
    if (typeof valor !== 'string' || valor.trim().length === 0) {
      problemas.push(campo(`${chave}: obrigatório e não vazio`));
    }
  }

  if (typeof frontmatter.operacao === 'string' &&
    frontmatter.operacao !== operacao.operationId) {
    problemas.push(campo(`operacao: "${frontmatter.operacao}" não é a operação desta ` +
      `página (${operacao.operationId})`));
  }

  const acesso = frontmatter.acesso;
  if (typeof acesso === 'string' && acesso.length > 0) {
    if (!Object.hasOwn(ACESSO_TEXTO, acesso)) {
      problemas.push(campo(
        `acesso: precisa ser um de ${Object.keys(ACESSO_TEXTO).join(', ')}`));
    } else {
      // publico/capacidade → security vazio; sessao/dono → cookieAuth; admin →
      // adminToken. O contrato autentica; o override autoriza.
      const forma = formaDeAutenticacao(operacao.operacao);
      const esperado = {
        publico: 'nenhuma', capacidade: 'nenhuma', sessao: 'sessao',
        dono: 'sessao', admin: 'admin'
      }[acesso];
      if (forma !== esperado) {
        problemas.push(campo(`acesso: ${acesso} exige security ` +
          `${esperado === 'nenhuma' ? 'vazio' : esperado}; ` +
          `${operacao.operationId} declara ${forma ?? 'forma desconhecida'}`));
      }
      if (acesso === 'capacidade') {
        const temToken = (operacao.operacao.parameters ?? []).some((parametro) =>
          (parametro.in === 'path' || parametro.in === 'query') &&
          parametro.name === 'shareToken') ||
          parametrosDe(operacao, 'query').some((parametro) => parametro.name === 'token');
        if (!temToken) {
          problemas.push(campo(
            'acesso: capacidade exige shareToken em parameters ou token na query'));
        }
      }
    }
  }

  problemas.push(...problemasDeCaminhoEConsulta(entrada, operacao).map(campo));
  problemas.push(...problemasDeCorpoEArquivos(entrada, operacao, contrato).map(campo));
  problemas.push(...problemasDeErros(entrada, operacao).map(campo));

  const relacionados = frontmatter.relacionados;
  if (relacionados !== undefined) {
    if (!Array.isArray(relacionados)) {
      problemas.push(campo('relacionados: precisa ser lista de operationId'));
    } else {
      const conhecidos = new Set(listarOperacoes(contrato)
        .map((item) => item.operationId));
      for (const item of relacionados) {
        if (typeof item !== 'string' || !conhecidos.has(item)) {
          problemas.push(campo(`relacionados: operação inexistente: ${item}`));
        }
      }
    }
  }

  if (typeof entrada.corpo === 'string') {
    if (temHeading(entrada.corpo)) {
      problemas.push(campo('corpo: heading não é permitido — as seções são fixas pela página'));
    }
    // R28: `marked` preserva HTML, e o site renderiza `notasHtml` como HTML.
    // Nenhuma seção do portal precisa de script no corpo.
    if (/<script/i.test(entrada.corpo)) {
      problemas.push(campo('corpo: <script> não é permitido — notas são conteúdo, não página'));
    }
  }

  return problemas;
}

// `relacionados` resolvido para o formato que o link da página usa.
function relacionadasDoOverride(frontmatter, contrato) {
  const itens = new Map(listarOperacoes(contrato).map((item) => [item.operationId, item]));
  return (frontmatter.relacionados ?? []).map((operationId) => {
    const item = itens.get(operationId);
    const dominio = DOMINIOS.find((candidato) => candidato.tag === item?.tag);
    return {
      operationId,
      titulo: item?.operacao.summary ?? '',
      dominio: dominio?.slug ?? null
    };
  });
}

function montarUrl(operacao, frontmatter, servidor) {
  const valores = frontmatter.caminho ?? {};
  const rota = operacao.rota.replace(/\{([^}]+)\}/g, (achado, nome) =>
    (nome in valores ? String(valores[nome]) : achado));
  const consulta = frontmatter.consulta ?? {};
  const partes = [];
  // A ordem é a de `parameters`, não a do frontmatter: o exemplo fica estável.
  for (const parametro of parametrosDe(operacao, 'query')) {
    if (!(parametro.name in consulta)) continue;
    partes.push(`${parametro.name}=${encodeURIComponent(String(consulta[parametro.name]))}`);
  }
  return `${servidor}${rota}${partes.length > 0 ? `?${partes.join('&')}` : ''}`;
}

// Linhas do `curl` na ordem do brief: flags, URL, credencial, corpo. `-i` só
// quando o leitor precisa ver cabeçalho (Location ou resposta 204 sem corpo).
export function montarCurl(operacao, override, servidor) {
  const frontmatter = override.frontmatter ?? {};
  const respostas = operacao.operacao.responses ?? {};
  const temLocation = Object.entries(respostas).some(([, resposta]) =>
    Boolean(resposta?.headers?.Location));
  const tem204 = Object.keys(respostas).includes('204');

  const linhas = [temLocation || tem204 ? 'curl -s -i' : 'curl -s'];
  if (operacao.metodo !== 'GET') linhas.push(`-X ${operacao.metodo}`);
  linhas.push(`"${montarUrl(operacao, frontmatter, servidor)}"`);

  const forma = formaDeAutenticacao(operacao.operacao);
  if (forma === 'sessao') linhas.push('-b cookies.txt');
  if (forma === 'admin') linhas.push('-H "X-Admin-Token: SEU_ADMIN_TOKEN"');

  if (typeof frontmatter.corpo === 'string') {
    linhas.push('-H "Content-Type: application/json"');
    linhas.push(`-d '${frontmatter.corpo.replaceAll("'", "'\\''")}'`);
  }
  for (const [campoDoArquivo, arquivo] of Object.entries(frontmatter.arquivos ?? {})) {
    linhas.push(`-F "${campoDoArquivo}=@${arquivo}"`);
  }
  return linhas;
}

const VALOR_DE_FORMATO = Object.freeze({
  uuid: '00000000-0000-4000-8000-000000000000',
  date: '2026-11-21',
  'date-time': '2026-11-21T09:00:00Z',
  uri: 'https://fisga.abson.tech/exemplo'
});

const NOTA_DE_NULO = 'este campo pode ser null';
const NOTA_DE_LISTA = 'a lista vem vazia quando não há itens';
const NOTA_DE_CICLO = 'ciclo no schema';

function tiposDe(schema) {
  return Array.isArray(schema?.type) ? schema.type : [schema?.type];
}

function ehAnulavel(schema, contrato) {
  if (!schema || typeof schema !== 'object') return false;
  if (tiposDe(schema).includes('null')) return true;
  return (schema.oneOf ?? []).some((ramo) =>
    tiposDe(resolverRef(ramo, contrato)).includes('null'));
}

function semRepetir(notas) {
  return [...new Set(notas)];
}

// Sentinela de enum: o valor que só existe para dizer "não informado". A
// escolha é o primeiro valor real; se todos forem sentinela, o primeiro do
// contrato (melhor o valor declarado do que um inventado).
function valorDeEnum(valores) {
  return valores.find((valor) => !String(valor).startsWith('Nao')) ?? valores[0];
}

// Esqueleto determinístico do schema: mostra a forma, nunca dado plausível.
// A ordem das decisões é a do brief — `$ref`, `oneOf`, `type` composto, `enum`,
// `format`, `type` —, e `example`/`examples` só entram em `montarRespostas`.
export function sintetizar(schema, contrato) {
  const notas = [];

  function passo(atual, profundidade, vistos) {
    if (profundidade > 8) return {};
    // R23: a guarda de ciclo é defesa contra contrato futuro. O `Set` carrega
    // o nome do `$ref` já resolvido no caminho; voltar a ele para.
    const nomeDoRef = atual?.$ref ? String(atual.$ref) : null;
    if (nomeDoRef && vistos.has(nomeDoRef)) {
      notas.push(NOTA_DE_CICLO);
      return {};
    }
    const proximos = nomeDoRef ? new Set([...vistos, nomeDoRef]) : vistos;
    const resolvido = resolverRef(atual, contrato);
    if (!resolvido || typeof resolvido !== 'object') return {};

    if (resolvido.oneOf) {
      const ramos = resolvido.oneOf.filter((ramo) =>
        !tiposDe(resolverRef(ramo, contrato)).includes('null'));
      const temNulo = ramos.length !== resolvido.oneOf.length;
      if (temNulo) notas.push(NOTA_DE_NULO);
      if (ramos.length === 0) return null;
      return passo(ramos[0], profundidade, proximos);
    }

    // `type` composto: `null` sai com nota; um tipo só desce com ele mesmo;
    // mais de um é a forma serializada de um número (`["number","string"]`,
    // `["integer","string"]`) — o valor real é `0`, não `'0'`.
    if (Array.isArray(resolvido.type)) {
      const restantes = resolvido.type.filter((tipo) => tipo !== 'null');
      if (restantes.length !== resolvido.type.length) notas.push(NOTA_DE_NULO);
      if (restantes.length === 1) {
        return passo({ ...resolvido, type: restantes[0] }, profundidade, proximos);
      }
      if (restantes.includes('string')) return 0;
    }

    if (Array.isArray(resolvido.enum)) return valorDeEnum(resolvido.enum);

    if (typeof resolvido.format === 'string') {
      if (resolvido.format === 'binary') return undefined;
      if (Object.hasOwn(VALOR_DE_FORMATO, resolvido.format)) {
        return VALOR_DE_FORMATO[resolvido.format];
      }
    }

    if (resolvido.type === 'string') return '';
    if (resolvido.type === 'integer' || resolvido.type === 'number') return 0;
    if (resolvido.type === 'boolean') return false;
    if (resolvido.type === 'array') {
      notas.push(NOTA_DE_LISTA);
      return [];
    }
    if (resolvido.type === 'object' || resolvido.properties) {
      return objeto(resolvido, profundidade, proximos);
    }
    return {};
  }

  function objeto(resolvido, profundidade, vistos) {
    const valor = {};
    for (const [chave, filho] of Object.entries(resolvido.properties ?? {})) {
      const sintetizado = passo(filho, profundidade + 1, vistos);
      // `binary` não aparece: a chave é omitida do exemplo.
      if (sintetizado !== undefined) valor[chave] = sintetizado;
    }
    return valor;
  }

  const valor = passo(schema, 0, new Set());
  return { valor, notas: semRepetir(notas) };
}

// Uma resposta por status, em ordem numérica, com o corpo já resolvido. O
// `sucesso` é o primeiro `2XX`/`3XX` da lista (mesmo objeto, não cópia) e o
// `>= 400` alimenta `errosDerivados` — a mesma regra do Task 2.
export function montarRespostas(operacao, contrato) {
  function respostaDeStatus(numero, resposta) {
    const status = String(numero);
    const conteudo = resposta?.content ?? {};
    const mediaTypes = Object.keys(conteudo);
    const notas = [];
    const primeiro = Object.entries(conteudo)[0];
    const mediaType = primeiro?.[0] ?? null;
    const schema = primeiro?.[1]?.schema ?? null;
    const exemplo = primeiro?.[1]?.examples
      ? Object.values(primeiro[1].examples)[0]?.value
      : primeiro?.[1]?.example;
    const exemploDoContrato = exemplo !== undefined;

    let corpo = null;
    if (Object.keys(conteudo).length === 0 || status === '204') {
      notas.push('resposta sem corpo');
    } else if (mediaType === 'text/html') {
      notas.push('a resposta é text/html; o portal não mostra o documento');
    } else if (mediaType === 'application/pdf') {
      notas.push('a resposta é application/pdf; o portal não mostra o arquivo');
    } else if (exemploDoContrato) {
      // R24: o exemplo do contrato vence; o sintetizador nem roda.
      corpo = JSON.stringify(exemplo, null, 2);
    } else if (schema) {
      const sintetizado = sintetizar(schema, contrato);
      notas.push(...sintetizado.notas);
      if (sintetizado.valor === undefined) {
        // R25: `format: 'binary'` na raiz também não vira bloco — o media type
        // nomeia o que a resposta é (hoje só o PDF chega aqui, e ele já parou na
        // regra de `application/pdf` acima).
        notas.push(`a resposta é ${mediaType}; o portal não mostra o arquivo`);
      } else {
        corpo = JSON.stringify(sintetizado.valor, null, 2);
      }
    }

    return {
      status,
      descricao: (resposta?.description ?? '').trim(),
      // R26: `2XX`/`3XX` é desfecho; `>= 400` é erro e alimenta a tabela.
      desfecho: numero >= 200 && numero < 400,
      mediaTypes,
      corpo,
      notas: semRepetir(notas),
      location: (status === '201' || status === '202') &&
        Boolean(resposta?.headers?.Location)
    };
  }

  const respostas = Object.entries(operacao?.responses ?? {})
    .map(([status, resposta]) => [Number(status), resposta])
    .filter(([status]) => Number.isInteger(status))
    .sort(([a], [b]) => a - b)
    .map(([numero, resposta]) => respostaDeStatus(numero, resposta));

  // `sucesso` é o próprio item da lista (mesma referência), não uma cópia: a
  // página não tem como divergir entre `sucesso` e `respostas`. Nenhuma das 54
  // operações tem dois `2XX`/`3XX`, então o primeiro por ordem numérica é
  // também o único; se o contrato mudar, o primeiro continua sendo a resposta.
  const sucesso = respostas.find((resposta) => resposta.desfecho) ?? null;
  return { respostas, sucesso, errosDerivados: derivarErros(operacao) };
}

// R9: objeto puro não vira linha, só desce. R12: array de primitivo emite uma
// linha só (`campo[]`); array de objeto emite `campo` + `campo[]` e desce nos
// `items`. Array anulável (`type: ['null', 'array']`) fecha em `campo` sem
// descer: o valor pode ser `null`, e os itens não descrevem o que existe.
// R13: `obrigatorio` sai do `required` do pai, carregado ao descer. R22: o teto
// de profundidade é `> 6` — o contrato atinge 6 em `exportarConta`.
export function montarTabelaDeSchema(schema, contrato) {
  const linhas = [];

  function linha(caminho, tipo, obrigatorio, anulavel, valores = null) {
    linhas.push({ caminho, tipo, obrigatorio, anulavel, valores });
  }

  function descer(atual, caminho, profundidade, obrigatorio, vistos) {
    if (profundidade > 6) return;
    if (atual?.$ref && vistos.has(atual.$ref)) return;
    const proximos = atual?.$ref ? new Set([...vistos, atual.$ref]) : vistos;
    const resolvido = resolverRef(atual, contrato);
    if (!resolvido || typeof resolvido !== 'object') return;

    if (Array.isArray(resolvido.oneOf)) {
      const temNulo = ehAnulavel(resolvido, contrato);
      const ramo = resolvido.oneOf.find((candidato) =>
        !tiposDe(resolverRef(candidato, contrato)).includes('null'));
      // `oneOf` com `null` é null OU o outro ramo: a linha carrega a nulidade,
      // que é a informação que justifica existir; descer no objeto repetiria o
      // pai sem ela e quebraria a contagem medida (MeResponse → 5 linhas).
      if (temNulo) {
        if (!caminho) return;
        const alvo = ramo ? resolverRef(ramo, contrato) : {};
        const tipoDoRamo = tiposDe(alvo).find((candidato) => candidato !== 'null');
        linha(caminho, tipoDoRamo ?? 'object', obrigatorio, true);
        return;
      }
      if (ramo) descer(ramo, caminho, profundidade, obrigatorio, proximos);
      return;
    }

    const tipos = tiposDe(resolvido);
    if (tipos.includes('array')) {
      const anulavel = tipos.includes('null');
      if (!caminho) {
        if (resolvido.items) descer(resolvido.items, '', profundidade, obrigatorio, proximos);
        return;
      }
      if (anulavel) {
        linha(caminho, 'array', obrigatorio, true);
        return;
      }
      const item = resolverRef(resolvido.items, contrato);
      const itemEhObjeto = Boolean(item?.properties) || item?.type === 'object';
      if (!itemEhObjeto) {
        linha(`${caminho}[]`, 'array', obrigatorio, false);
        return;
      }
      linha(caminho, 'array', obrigatorio, false);
      linha(`${caminho}[]`, 'object', obrigatorio, false);
      descer(resolvido.items, `${caminho}[]`, profundidade + 1, false, proximos);
      return;
    }

    if (resolvido.properties) {
      const exigidos = new Set(resolvido.required ?? []);
      for (const [chave, filho] of Object.entries(resolvido.properties)) {
        descer(
          filho,
          caminho ? `${caminho}.${chave}` : chave,
          profundidade + 1,
          exigidos.has(chave),
          proximos);
      }
      return;
    }

    // Escalar (ou enum, que é escalar com lista): é aqui que a linha nasce.
    // `binary` é `string` no contrato e a tabela descreve a forma, não o valor:
    // o campo aparece (quem esconde o arquivo é o esqueleto, não a tabela).
    if (!caminho) return;
    if (Array.isArray(resolvido.enum)) {
      linha(caminho, 'enum', obrigatorio, ehAnulavel(resolvido, contrato), [...resolvido.enum]);
      return;
    }
    const tipo = tipos.find((candidato) => candidato !== 'null') ?? 'object';
    linha(caminho, tipo, obrigatorio, ehAnulavel(resolvido, contrato));
  }

  descer(schema, '', 0, false, new Set());
  return linhas;
}

// Tabela do corpo JSON do requestBody; `null` quando não há corpo JSON (é o
// caso de multipart, onde o dono do exemplo é `arquivos`).
function tabelaDoRequest(operacao, contrato) {
  const conteudo = operacao.requestBody?.content ?? {};
  const schema = conteudo['application/json']?.schema;
  return schema ? montarTabelaDeSchema(schema, contrato) : null;
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

// O valor de exemplo vem do campo certo: `caminho` para `in: path` e `consulta`
// para `in: query`. Header e cookie não têm valor de exemplo no override.
function valorDoParametro(parametro, caminho, consulta) {
  const valores = parametro.in === 'path'
    ? caminho
    : (parametro.in === 'query' ? consulta : {});
  return parametro.name in valores ? valores[parametro.name] : null;
}

function errosDoModelo(operacao, frontmatter) {
  const derivados = derivarErros(operacao);
  const doOverride = (frontmatter.erros ?? []).map((erro) => ({
    status: erro.status,
    codigos: listaDeCodigos(erro.codigos ?? erro.codigo),
    quando: erro.quando,
    origem: 'override'
  }));
  // Ordenados por status: é a ordem em que a página mostra a tabela.
  return [...derivados, ...doOverride].sort((a, b) => a.status - b.status);
}

function derivarOperacao({ operationId, metodo, rota, tag, operacao }, dominio, consentimento,
  override, contrato) {
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
  const frontmatter = override.frontmatter;
  const acesso = frontmatter.acesso;
  const servidor = contrato.servers?.[0]?.url ?? '';
  const caminho = frontmatter.caminho ?? {};
  const consulta = frontmatter.consulta ?? {};
  const { respostas, sucesso } = montarRespostas(operacao, contrato);
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
      proposta: frontmatter.proposta,
      acesso,
      acessoTexto: ACESSO_TEXTO[acesso] ?? null,
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
        obrigatorio: parametro.required === true,
        valor: valorDoParametro(parametro, caminho, consulta)
      })),
      erros: errosDoModelo(operacao, frontmatter),
      exemplo: montarCurl({ operationId, metodo, rota, operacao }, override, servidor),
      verificar: frontmatter.verificar,
      // O corpo é opcional e não gera linha: string vazia renderiza nada.
      notas: typeof override.corpo === 'string' ? override.corpo : '',
      notasHtml: typeof override.corpo === 'string' && override.corpo.length > 0
        ? marked.parse(override.corpo)
        : '',
      relacionados: relacionadasDoOverride(frontmatter, contrato),
      // O contrato responde e o override só exemplifica: a página mostra o
      // esqueleto derivado do schema, rotulado, e os erros derivados.
      respostas,
      sucesso,
      // R1: o request é o único dono do corpo.
      request: {
        corpoTabela: tabelaDoRequest(operacao, contrato),
        corpoExemplo: typeof frontmatter.corpo === 'string' ? frontmatter.corpo : null,
        arquivos: frontmatter.arquivos ?? null
      }
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

    const override = overrides.get(operationId);
    if (!override) {
      problemas.push(`${operationId}: override: falta ${caminhoDoOverride(operationId)}`);
      continue;
    }

    // `security` fora das três formas é problema do contrato, não do override:
    // é ele que impede classificar o acesso.
    if (formaDeAutenticacao(operacao) === null) {
      problemas.push(`${operationId}: security: security não reconhecido ` +
        `(${JSON.stringify(operacao.security)})`);
      continue;
    }

    // O override só entra no modelo depois de passar nas regras: um exemplo
    // inválido não pode virar página.
    const problemasDoOverride = validarOverride(override, item, contrato);
    if (problemasDoOverride.length > 0) {
      problemas.push(...problemasDoOverride);
      continue;
    }

    const resultado = derivarOperacao(item, dominio, consentimento, override, contrato);
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

// Os overrides vêm do disco, mas `lerOverrides` trata diretório ausente como
// lista vazia: hoje, sem nenhum arquivo, a lista de pendências é a cobertura.
// Nenhum JSON parcial é gravado: o arquivo só existe quando o modelo está
// completo.
function main() {
  let modelo;
  try {
    const overrides = new Map(lerOverrides().map((entrada) => [entrada.operacao, entrada]));
    modelo = gerarModelo(lerContrato(), overrides);
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
