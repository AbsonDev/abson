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

// O `requestBody` declara o schema por `$ref` em `components.schemas`. Segue a
// cadeia (com guarda de ciclo) só para ler `required` e as propriedades.
function schemaDoCorpo(schema, contrato) {
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
          const schema = schemaDoCorpo(conteudo['application/json'].schema, contrato);
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
      const schema = schemaDoCorpo(conteudo['multipart/form-data'].schema, contrato);
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
      // R1: o request é o único dono do corpo; o Task 4 acrescenta `corpoTabela`.
      request: {
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
