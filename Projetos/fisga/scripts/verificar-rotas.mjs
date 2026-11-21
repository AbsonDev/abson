#!/usr/bin/env node
// Compara as rotas do contrato com o resumo do README e com o inventário.
// O contrato é a fonte; README e inventário são texto mantido à mão e por isso
// podem divergir sem ninguém notar. Roda no job `docs`.
import { existsSync, readFileSync, realpathSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';

const METODOS = ['get', 'post', 'put', 'patch', 'delete'];
const LINHA_DE_OPERACAO = /^`(GET|POST|PUT|PATCH|DELETE) (\/\S+)`$/;

export function extrairDoContrato(documento) {
  const rotas = new Set();
  for (const [rota, operacoes] of Object.entries(documento.paths ?? {})) {
    for (const metodo of Object.keys(operacoes)) {
      if (METODOS.includes(metodo)) {
        rotas.add(`${metodo.toUpperCase()} ${rota}`);
      }
    }
  }
  return rotas;
}

export function extrairDoReadme(markdown) {
  const rotas = new Set();
  for (const linha of linhasDaSecao(markdown, '## API')) {
    const celulas = linha.split('|').map((celula) => celula.trim());
    if (celulas.length < 4) continue;
    const metodo = celulas[1].replaceAll('`', '');
    if (!METODOS.includes(metodo.toLowerCase())) continue;
    for (const bruta of celulas[2].split(',')) {
      const rota = bruta.trim().replaceAll('`', '').split('?')[0];
      if (rota.startsWith('/')) rotas.add(`${metodo.toUpperCase()} ${rota}`);
    }
  }
  return rotas;
}

export function extrairDoInventario(markdown) {
  const rotas = new Set();
  for (const linha of linhasDaSecao(markdown, '## Domínios')) {
    const celulas = linha.split('|').map((celula) => celula.trim());
    if (celulas.length < 3) continue;
    const encontrada = celulas[1].match(LINHA_DE_OPERACAO);
    if (encontrada) rotas.add(`${encontrada[1]} ${encontrada[2].split('?')[0]}`);
  }
  return rotas;
}

export function divergencias(contrato, readme, inventario) {
  const ordenar = (conjunto) => [...conjunto].sort();
  return {
    faltamNoReadme: ordenar([...contrato].filter((rota) => !readme.has(rota))),
    faltamNoInventario: ordenar([...contrato].filter((rota) => !inventario.has(rota))),
    sobramNoReadme: ordenar([...readme].filter((rota) => !contrato.has(rota))),
    sobramNoInventario: ordenar([...inventario].filter((rota) => !contrato.has(rota)))
  };
}

// Seções do Markdown vão da linha igual ao título (tolerando CRLF e espaço
// irregular) até a próxima linha de nível 2 (exclusive). Uma tabela fora da
// seção não conta.
function linhasDaSecao(markdown, titulo) {
  const linhas = markdown.split(/\r?\n/);
  const nome = titulo.replace(/^##\s+/, '');
  const cabecalho = new RegExp(`^##\\s+${escaparRegex(nome)}\\s*$`);
  const inicio = linhas.findIndex((linha) => cabecalho.test(linha));
  if (inicio === -1) return [];
  const fim = linhas.findIndex(
    (linha, indice) => indice > inicio && linha.startsWith('## '));
  return linhas.slice(inicio + 1, fim === -1 ? linhas.length : fim);
}

function escaparRegex(texto) {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function lerDaRaiz(caminho) {
  const raiz = new URL('..', import.meta.url);
  return readFileSync(fileURLToPath(new URL(caminho, raiz)), 'utf8');
}

function executar() {
  const contrato = extrairDoContrato(JSON.parse(lerDaRaiz('docs/api/openapi.json')));
  const readme = extrairDoReadme(lerDaRaiz('README.md'));
  const inventario = extrairDoInventario(lerDaRaiz('docs/api/inventario.md'));

  // Conjunto vazio "não diverge", então um extrator quebrado passaria em
  // silêncio. Documento sem nenhuma rota lida é falha, não sincronia.
  const vazios = [
    [contrato, 'o contrato (docs/api/openapi.json)'],
    [readme, 'o README.md (seção "## API")'],
    [inventario, 'o inventário (docs/api/inventario.md, seção "## Domínios")']
  ].filter(([rotas]) => rotas.size === 0).map(([, nome]) => nome);

  if (vazios.length > 0) {
    console.error(
      `Documento sem nenhuma rota lida: ${vazios.join(', ')}. ` +
        'O extrator ou o formato do documento mudou.');
    process.exitCode = 1;
    return;
  }

  const resultado = divergencias(contrato, readme, inventario);
  const secoes = [
    ['Faltam no README.md:', resultado.faltamNoReadme],
    ['Faltam no inventário:', resultado.faltamNoInventario],
    ['Sobram no README.md (não estão no contrato):', resultado.sobramNoReadme],
    ['Sobram no inventário (não estão no contrato):', resultado.sobramNoInventario]
  ].filter(([, rotas]) => rotas.length > 0);

  if (secoes.length > 0) {
    for (const [titulo, rotas] of secoes) {
      console.error(titulo);
      for (const rota of rotas) console.error(`  - ${rota}`);
    }
    process.exitCode = 1;
    return;
  }

  console.log(
    `Rotas em sincronia: ${contrato.size} operações no contrato, no README e no inventário.`);
}

// Importado pelos testes, o módulo só exporta as funções puras. O teste de
// subprocesso garante que esta detecção não regride para um no-op verde.
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
  executar();
}
