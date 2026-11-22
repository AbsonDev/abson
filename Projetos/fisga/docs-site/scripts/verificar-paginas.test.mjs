// Teste pós-build das 69 páginas do portal de documentação.
// Consome dist/ e src/data/referencia.json.
// Falha ruidosa se dist/ não existir ou se faltar qualquer página/invariante.

import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import {
  existsSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const DIR_SCRIPT = fileURLToPath(new URL('.', import.meta.url));
const DIR_DIST = join(DIR_SCRIPT, '../dist');
const ARQUIVO_MODELO = join(DIR_SCRIPT, '../src/data/referencia.json');

if (!existsSync(DIR_DIST)) {
  throw new Error('rode "npm run build" antes de "npm run test:paginas"');
}

const modelo = JSON.parse(readFileSync(ARQUIVO_MODELO, 'utf8'));

function listarHtmls(dir, base = '') {
  const htmls = [];
  const entries = readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const rel = base ? `${base}/${entry.name}` : entry.name;
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      htmls.push(...listarHtmls(full, rel));
    } else if (entry.name.endsWith('.html')) {
      htmls.push(rel);
    }
  }
  return htmls;
}

function unescapeHtml(s) {
  return s
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>');
}

const htmlsGerados = listarHtmls(DIR_DIST);

test('exatamente 69 páginas HTML geradas e nenhuma além das esperadas', () => {
  const paginasEsperadas = new Set([
    'index.html',
    'referencia.html',
    ...modelo.dominios.map((d) => `referencia/${d.slug}.html`),
    ...modelo.dominios.flatMap((d) =>
      d.operacoes.map((opId) => `referencia/${d.slug}/${opId}.html`)
    )
  ]);

  assert.equal(
    paginasEsperadas.size,
    69,
    `esperadas 69 páginas, encontrou ${paginasEsperadas.size}`
  );
  assert.equal(
    htmlsGerados.length,
    69,
    `dist/ possui ${htmlsGerados.length} páginas HTML, esperado 69`
  );

  const geradosSet = new Set(htmlsGerados);
  for (const esperada of paginasEsperadas) {
    assert.ok(
      geradosSet.has(esperada),
      `página esperada ausente em dist/: ${esperada}`
    );
  }
  for (const gerada of htmlsGerados) {
    assert.ok(
      paginasEsperadas.has(gerada),
      `página espúria/inesperada em dist/: ${gerada}`
    );
  }
});

test('toda página respeita invariantes estruturais, canônicos e acessibilidade', () => {
  for (const rel of htmlsGerados) {
    const caminhoCompleto = join(DIR_DIST, rel);
    const html = readFileSync(caminhoCompleto, 'utf8');

    // 1. Idioma
    assert.ok(
      html.includes('lang="pt-BR"'),
      `${rel} deve declarar lang="pt-BR"`
    );

    // 2. Exatamente um <h1>
    const h1Matches = html.match(/<h1(\s|>)/gi);
    assert.equal(
      h1Matches?.length,
      1,
      `${rel} deve ter exatamente um <h1>, encontrou ${h1Matches?.length}`
    );

    // 3. <title> não vazio
    const titleMatch = html.match(/<title>([^<]+)<\/title>/i);
    assert.ok(
      titleMatch && titleMatch[1].trim().length > 0,
      `${rel} deve conter <title> não vazio`
    );

    // 4. Região <main> e skip link
    assert.ok(html.includes('<main'), `${rel} deve conter tag <main>`);
    assert.ok(
      html.includes('<a class="skip-link"'),
      `${rel} deve conter link de pular para o conteúdo principal`
    );

    // 5. Nenhum script sem src=
    const scripts = html.match(/<script\b[^>]*>/gi) || [];
    for (const scriptTag of scripts) {
      assert.ok(
        scriptTag.includes('src='),
        `${rel} contém <script> sem src: ${scriptTag}`
      );
    }

    // 6. Nenhum style inline com cor literal
    assert.equal(
      /style="[^"]*(#[0-9a-fA-F]{3,8}|rgb\()/i.test(html),
      false,
      `${rel} contém style="" com cor literal`
    );

    // 7. Canonical correto sem .html
    const caminhoSemHtml =
      rel === 'index.html' ? '/' : `/${rel.replace(/\.html$/, '')}`;
    const urlEsperada = `https://docs.fisga.abson.tech${caminhoSemHtml}`;
    const canonicalMatch = html.match(
      /<link rel="canonical" href="([^"]+)"/
    );
    assert.ok(canonicalMatch, `${rel} deve conter <link rel="canonical">`);
    assert.equal(
      canonicalMatch[1],
      urlEsperada,
      `${rel} canonical incorreto: ${canonicalMatch[1]}`
    );
    assert.equal(
      canonicalMatch[1].includes('.html'),
      false,
      `${rel} canonical não pode conter .html: ${canonicalMatch[1]}`
    );

    // 8. Exatamente um CSS referenciado em _astro/
    const cssMatches = html.match(/href="[^"]*\/_astro\/[^"]*\.css"/g);
    assert.equal(
      cssMatches?.length,
      1,
      `${rel} deve referenciar exatamente 1 css de _astro/, encontrou ${cssMatches?.length}`
    );

    // 9. Nenhum .woff2 referenciado diretamente no HTML
    assert.equal(
      html.includes('.woff2'),
      false,
      `${rel} referencia .woff2 fora do CSS`
    );
  }
});

test('página inicial contém título da API e versão do contrato', () => {
  const html = readFileSync(join(DIR_DIST, 'index.html'), 'utf8');
  assert.ok(
    html.includes(modelo.info.titulo),
    `index.html deve conter título "${modelo.info.titulo}"`
  );
  assert.ok(
    html.includes(modelo.info.versao),
    `index.html deve conter versão "${modelo.info.versao}"`
  );
});

test('páginas de domínio listam todas as suas operações', () => {
  for (const dominio of modelo.dominios) {
    const html = readFileSync(
      join(DIR_DIST, `referencia/${dominio.slug}.html`),
      'utf8'
    );
    for (const opId of dominio.operacoes) {
      const linkEsperado = `/referencia/${dominio.slug}/${opId}`;
      assert.ok(
        html.includes(linkEsperado),
        `referencia/${dominio.slug}.html deve linkar para ${linkEsperado}`
      );
    }
  }
});

test('páginas de operação cumprem todos os rulings e invariantes editoriais', () => {
  const tmpDir = mkdtempSync(join(tmpdir(), 'verificar-paginas-r44-'));

  try {
    const dominIoPorTag = new Map(modelo.dominios.map((d) => [d.tag, d]));

    for (const [opId, op] of Object.entries(modelo.operacoes)) {
      const dominio = dominIoPorTag.get(op.tag);
      assert.ok(dominio, `domínio não encontrado para tag ${op.tag}`);

      const relPath = `referencia/${dominio.slug}/${opId}.html`;
      const html = readFileSync(join(DIR_DIST, relPath), 'utf8');

      // 1. ID da operação no <h1>
      assert.ok(
        new RegExp(`<h1[^>]*id="${opId}"`, 'i').test(html),
        `${relPath} deve conter id="${opId}" no <h1>`
      );

      // 2. Proposta do override na página
      const propostaLimpa = op.proposta.replace(/\s+/g, ' ').trim();
      assert.ok(
        html.includes(propostaLimpa),
        `${relPath} deve conter o texto de proposta`
      );

      // 3. Bloco <pre
      assert.ok(html.includes('<pre'), `${relPath} deve conter bloco <pre>`);

      // 4. Aviso de mutação em métodos de escrita
      const avisoMutacao =
        'Estes exemplos apontam para produção e alteram dados reais da conta.';
      if (op.metodo !== 'GET') {
        assert.ok(
          html.includes(avisoMutacao),
          `${relPath} (${op.metodo}) deve conter aviso de mutação de produção`
        );
      } else {
        assert.equal(
          html.includes(avisoMutacao),
          false,
          `${relPath} (GET) não pode conter aviso de mutação`
        );
      }

      // 5. Aviso de sessão "Antes de chamar"
      if (op.autenticacao === 'sessao') {
        assert.ok(
          html.includes('Antes de chamar'),
          `${relPath} (autenticação de sessão) deve conter aviso "Antes de chamar"`
        );
      } else {
        assert.equal(
          html.includes('Antes de chamar'),
          false,
          `${relPath} não pode conter aviso "Antes de chamar"`
        );
      }

      // 6. Tabela de erros vs sem erro declarado
      if (op.erros.length > 0) {
        assert.ok(
          html.includes('class="tabela-erros"'),
          `${relPath} deve renderizar tabela-erros`
        );
      } else {
        assert.ok(
          html.includes('Sem erro declarado no contrato'),
          `${relPath} sem erros deve declarar "Sem erro declarado no contrato"`
        );
      }

      // 7. Ruling R47: 302 nunca aparece na seção de erros
      const secaoErrosMatch = html.match(
        /<section\b[^>]*id="erros"[^>]*>([\s\S]*?)<\/section>/i
      );
      assert.ok(secaoErrosMatch, `${relPath} deve ter seção id="erros"`);
      assert.equal(
        secaoErrosMatch[1].includes('302'),
        false,
        `${relPath} seção de erros não pode conter status 302`
      );

      // 8. Rulings R46 / R53: Header <code>Location</code> exclusivo quando declarado
      const marcaLocation = 'Header <code>Location</code>';
      if (op.sucesso?.location === true) {
        assert.ok(
          html.includes(marcaLocation),
          `${relPath} com location: true deve conter marca "${marcaLocation}"`
        );
      } else {
        assert.equal(
          html.includes(marcaLocation),
          false,
          `${relPath} sem location: true não pode conter "${marcaLocation}"`
        );
      }

      // 9. Exemplo derivado do schema rotulado
      const rotuloSchema = 'Exemplo derivado do schema';
      if (op.sucesso?.corpo !== null) {
        assert.ok(
          html.includes(rotuloSchema),
          `${relPath} com corpo de resposta deve conter rótulo "${rotuloSchema}"`
        );
      } else {
        assert.equal(
          html.includes(rotuloSchema),
          false,
          `${relPath} sem corpo de resposta não pode conter rótulo "${rotuloSchema}"`
        );
      }

      // 10. Media types especiais
      const mediaTypes = op.sucesso?.mediaTypes ?? [];
      if (mediaTypes.includes('text/html')) {
        assert.ok(
          html.includes('text/html'),
          `${relPath} deve exibir text/html`
        );
      }
      if (mediaTypes.includes('application/pdf')) {
        assert.ok(
          html.includes('application/pdf'),
          `${relPath} deve exibir application/pdf`
        );
      }

      // 11. Consentimento vigente
      const textoConsentimento = 'consentimento vigente';
      if (op.consentimento === true) {
        assert.ok(
          html.includes(textoConsentimento),
          `${relPath} (consentimento: true) deve conter "${textoConsentimento}"`
        );
      } else {
        assert.equal(
          html.includes(textoConsentimento),
          false,
          `${relPath} (consentimento: false) não pode conter "${textoConsentimento}"`
        );
      }

      // 12. Rate limit declarado
      const textoSemRateLimit = 'sem política de rate limit declarada';
      if (op.limites === null) {
        assert.ok(
          html.includes(textoSemRateLimit),
          `${relPath} sem limites deve conter "${textoSemRateLimit}"`
        );
      } else {
        assert.equal(
          html.includes(textoSemRateLimit),
          false,
          `${relPath} com limites não pode conter "${textoSemRateLimit}"`
        );
      }

      // 13. Relacionados
      if (op.relacionados && op.relacionados.length > 0) {
        for (const rel of op.relacionados) {
          const linkRelEsperado = `/${rel.operationId}`;
          assert.ok(
            html.includes(linkRelEsperado),
            `${relPath} deve conter link para relacionado ${linkRelEsperado}`
          );
        }
      } else {
        assert.equal(
          html.includes('class="relacionados-lista"'),
          false,
          `${relPath} sem relacionados não pode renderizar lista de relacionados`
        );
      }

      // 14. Ruling R44: Sintaxe de shell bash no curl com \ no fim de linha
      const bashMatch = html.match(
        /<pre><code class="language-bash">([\s\S]*?)<\/code><\/pre>/
      );
      assert.ok(bashMatch, `${relPath} deve conter bloco language-bash`);
      const bashCode = unescapeHtml(bashMatch[1].trim());
      const linhasBash = bashCode.split('\n');

      assert.ok(
        linhasBash[0].startsWith('curl '),
        `${relPath} primeira linha do exemplo bash deve iniciar com "curl "`
      );

      for (let i = 0; i < linhasBash.length - 1; i++) {
        assert.ok(
          linhasBash[i].endsWith('\\'),
          `${relPath} linha ${i} de bash deve terminar com "\\", obteve: "${linhasBash[i]}"`
        );
      }
      assert.equal(
        linhasBash[linhasBash.length - 1].endsWith('\\'),
        false,
        `${relPath} última linha de bash não pode terminar com "\\"`
      );

      const scriptSh = join(tmpDir, `${opId}.sh`);
      writeFileSync(scriptSh, bashCode, 'utf8');
      try {
        execFileSync('bash', ['-n', scriptSh]);
      } catch (err) {
        assert.fail(
          `${relPath} exemplo bash falhou na sintaxe bash -n: ${err.message}`
        );
      }
    }
  } finally {
    rmSync(tmpDir, { recursive: true, force: true });
  }
});
