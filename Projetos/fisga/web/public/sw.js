const VERSAO = 'fisga-v7';
const CASCA = [
  '/',
  '/entrar',
  '/meu-diario',
  '/registrar',
  '/condicoes',
  '/mapa',
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

self.addEventListener('install', (evento) => {
  evento.waitUntil((async () => {
    const cache = await caches.open(VERSAO);
    await cache.addAll(CASCA);

    // A wiki é estática e feita para ser lida offline. O build gera a lista
    // de páginas e arquivos do Pagefind; se falhar, o app continua online.
    try {
      const resposta = await fetch('/wiki-precache.json', { cache: 'no-cache' });
      if (resposta.ok) {
        const urls = await resposta.json();
        await Promise.allSettled(urls.map((url) => cache.add(url)));
      }
    } catch {
      // Sem precache da wiki nesta instalação.
    }

    await self.skipWaiting();
  })());
});

self.addEventListener('activate', (evento) => {
  evento.waitUntil(
    caches.keys()
      .then((chaves) => Promise.all(
        chaves
          .filter((chave) => chave !== VERSAO && chave !== 'fisga-tiles-v1')
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
    const privada = ['/locais', '/conta', '/meu-diario', '/registrar', '/trofeu'].some((rota) =>
      url.pathname.startsWith(rota)
    );
    if (privada) {
      evento.respondWith(fetch(evento.request));
      return;
    }

    evento.respondWith(
      fetch(evento.request)
        .then((resposta) => {
          const copia = resposta.clone();
          caches.open(VERSAO).then((cache) => cache.put(evento.request, copia));
          return resposta;
        })
        .catch(async () => {
          const url = new URL(evento.request.url);
          const comBarra = url.pathname.endsWith('/') ? url.pathname : `${url.pathname}/`;
          const semBarra = comBarra.replace(/\/$/, '');
          return (
            (await caches.match(evento.request)) ||
            (await caches.match(new URL(comBarra, url.origin).toString())) ||
            (await caches.match(new URL(semBarra, url.origin).toString())) ||
            caches.match('/offline')
          );
        })
    );
    return;
  }

  const hostsDeTiles = [
    'tile.openstreetmap.org',
    'server.arcgisonline.com',
    'tiles.openseamap.org',
    'wms.gebco.net'
  ];

  if (hostsDeTiles.some((host) => url.hostname.endsWith(host))) {
    evento.respondWith((async () => {
      const cache = await caches.open('fisga-tiles-v1');
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
        const copia = resposta.clone();
        caches.open(VERSAO).then((cache) => cache.put(evento.request, copia));
        return resposta;
      } catch {
        return new Response('', { status: 504, statusText: 'Offline' });
      }
    })());
  }
});
