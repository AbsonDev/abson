const VERSAO = 'fisga-v9';
// O cache público usa o sufixo da versão (`fisga-public-v9`); os dois mudam
// juntos, sem repetir o número em dois lugares.
const CACHE_PUBLICO = `fisga-public-${VERSAO.replace(/^fisga-/, '')}`;
const CACHE_TILES = 'fisga-tiles-v1';

// Políticas compartilhadas com os testes (`podeCachear`, `rotaPrivada`,
// `paginaPublica`). `importScripts` é síncrono e precisa ficar no topo.
importScripts('/sw-politicas.js');
const { rotaPrivada, paginaPublica, podeCachear, PAGINAS_PUBLICAS } = self.FisgaSWPoliticas;

// Só conteúdo público e estável entra na instalação. `/`, `/condicoes`,
// `/meu-diario` e `/mapa` dependem da sessão e são tratados em runtime.
const CASCA = [
  '/entrar',
  '/aprender',
  '/ferramentas',
  '/ferramentas/nos',
  '/ferramentas/checklists',
  '/ferramentas/defeso',
  '/ferramentas/guias',
  '/precos',
  '/offline',
  '/wiki',
  '/wiki/buscar',
  '/manifest.webmanifest',
  '/favicon.svg',
  '/icon-192.png',
  '/icon-512.png',
  '/og-fisga.png'
];

const HOSTS_DE_TILES = [
  'tile.openstreetmap.org',
  'server.arcgisonline.com',
  'tiles.openseamap.org',
  'wms.gebco.net'
];

/** Grava em segundo plano; a resposta original segue para a página. */
function guardarNoCache(requisicao, resposta) {
  if (!podeCachear(resposta)) return;

  const copia = resposta.clone();
  caches
    .open(CACHE_PUBLICO)
    .then((cache) => cache.put(requisicao, copia))
    .catch(() => {
      // Cache cheio ou indisponível: a página continua funcionando online.
    });
}

async function paginaOffline() {
  const resposta = await caches.match('/offline');
  return resposta || new Response('', { status: 504, statusText: 'Offline' });
}

self.addEventListener('install', (evento) => {
  evento.waitUntil((async () => {
    const cache = await caches.open(CACHE_PUBLICO);

    // Instala só o que se declara público. `cache.addAll` gravaria qualquer
    // resposta, inclusive uma página personalizada por cookie; o fetch sem
    // credenciais + podeCachear mantém a mesma regra do runtime e preserva a
    // invariante de que `fisga-public-*` só tem conteúdo aprovado.
    await Promise.allSettled(
      CASCA.map(async (url) => {
        const resposta = await fetch(url, { credentials: 'omit', cache: 'no-cache' });
        if (podeCachear(resposta)) {
          await cache.put(url, resposta);
        }
      })
    );

    // A wiki é estática e feita para ser lida offline. O build gera a lista
    // de páginas e arquivos do Pagefind; se falhar, o app continua online.
    try {
      const resposta = await fetch('/wiki-precache.json', { cache: 'no-cache' });
      if (resposta.ok) {
        const urls = await resposta.json();
        await Promise.allSettled(
          urls.map(async (url) => {
            const pagina = await fetch(url, { credentials: 'omit', cache: 'no-cache' });
            if (podeCachear(pagina)) {
              await cache.put(url, pagina);
            }
          })
        );
      }
    } catch {
      // Sem precache da wiki nesta instalação.
    }

    // O shell de `/` e `/condicoes` só é aquecido sem cookie. Se o servidor
    // (ou um proxy adiantado) responder privado, a página fica só online.
    await Promise.allSettled(
      PAGINAS_PUBLICAS.map(async (url) => {
        const resposta = await fetch(url, { credentials: 'omit', cache: 'no-cache' });
        if (podeCachear(resposta)) {
          await cache.put(url, resposta);
        }
      })
    );

    await self.skipWaiting();
  })());
});

self.addEventListener('activate', (evento) => {
  evento.waitUntil(
    caches.keys()
      .then((chaves) => Promise.all(
        chaves
          .filter((chave) => chave !== CACHE_PUBLICO && chave !== CACHE_TILES)
          .map((chave) => caches.delete(chave))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (evento) => {
  if (evento.request.method !== 'GET') return;

  const url = new URL(evento.request.url);

  // Dados privados e mutações nunca entram no cache.
  if (url.pathname.startsWith('/api/')) return;

  if (evento.request.mode === 'navigate') {
    // Rota privada nunca é gravada nem lida do cache: offline cai no aviso,
    // nunca no HTML de outra sessão.
    if (rotaPrivada(url.pathname)) {
      evento.respondWith(fetch(evento.request).catch(() => paginaOffline()));
      return;
    }

    evento.respondWith(
      fetch(evento.request)
        .then((resposta) => {
          // Só o shell que varia com a sessão precisa de cache em runtime; as
          // demais páginas públicas já vêm da instalação. Assim o logout não
          // recria o cache ao cair em `/entrar`.
          if (url.origin === self.location.origin && paginaPublica(url.pathname)) {
            guardarNoCache(evento.request, resposta);
          }
          return resposta;
        })
        .catch(async () => {
          const comBarra = url.pathname.endsWith('/') ? url.pathname : `${url.pathname}/`;
          const semBarra = comBarra.replace(/\/$/, '');
          return (
            (await caches.match(evento.request)) ||
            (await caches.match(new URL(comBarra, url.origin).toString())) ||
            (await caches.match(new URL(semBarra, url.origin).toString())) ||
            (await paginaOffline())
          );
        })
    );
    return;
  }

  if (HOSTS_DE_TILES.some((host) => url.hostname.endsWith(host))) {
    evento.respondWith((async () => {
      const cache = await caches.open(CACHE_TILES);
      const salvo = await cache.match(evento.request);
      if (salvo) return salvo;

      try {
        const resposta = await fetch(evento.request);
        if (resposta.ok || resposta.type === 'opaque') {
          await cache.put(evento.request, resposta.clone());
          const chaves = await cache.keys();
          if (chaves.length > 600) {
            await cache.delete(chaves[0]);
          }
        }
        return resposta;
      } catch {
        return salvo || new Response('', { status: 504, statusText: 'Offline' });
      }
    })());
    return;
  }

  if (url.origin === self.location.origin) {
    evento.respondWith((async () => {
      const emCache = await caches.match(evento.request);
      if (emCache) return emCache;

      // O Pagefind usa ?ts= para furar o cache do navegador. O conteúdo é o
      // mesmo arquivo já precacheado sem a query.
      if (url.pathname.startsWith('/pagefind/')) {
        const semQuery = await caches.match(new URL(url.pathname, url.origin).toString());
        if (semQuery) return semQuery;
      }

      try {
        const resposta = await fetch(evento.request);
        guardarNoCache(evento.request, resposta);
        return resposta;
      } catch {
        return new Response('', { status: 504, statusText: 'Offline' });
      }
    })());
  }
});
