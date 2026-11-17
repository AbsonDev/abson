import { readdir, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';

const raiz = 'dist/client';
const urls = new Set();

async function caminhar(diretorio) {
  let entradas;
  try {
    entradas = await readdir(diretorio, { withFileTypes: true });
  } catch {
    return;
  }

  for (const entrada of entradas) {
    const caminho = join(diretorio, entrada.name);
    if (entrada.isDirectory()) {
      await caminhar(caminho);
      continue;
    }

    if (!entrada.name.endsWith('.html') && !diretorio.includes('pagefind')) {
      continue;
    }

    const relativo = relative(raiz, caminho).split('\\').join('/');
    if (relativo.endsWith('.html')) {
      const limpo = relativo
        .replace(/index\.html$/, '')
        .replace(/\.html$/, '/');
      urls.add(`/${limpo}`.replace(/\/+/g, '/'));
    } else {
      urls.add(`/${relativo}`);
    }
  }
}

await caminhar(join(raiz, 'wiki'));
await caminhar(join(raiz, 'pagefind'));

const lista = [...urls].sort();
await writeFile(join(raiz, 'wiki-precache.json'), JSON.stringify(lista, null, 2));
console.log(`precache da wiki: ${lista.length} URLs`);
