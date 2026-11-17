const VERSAO = 'fisga-v3';
const CASCA = [
  '/',
  '/entrar',
  '/meu-diario',
  '/registrar',
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
      .then((chaves) => Promise.all(chaves.filter((chave) => chave !== VERSAO).map((chave) => caches.delete(chave))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (evento) => {
  if (evento.request.method !== 'GET') return;

  const url = new URL(evento.request.url);

  // Dados privados e mutações nunca entram no cache.
  if (url.pathname.startsWith('/api/')) return;

  if (evento.request.mode === 'navigate') {
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

  if (url.origin === self.location.origin) {
    evento.respondWith(
      caches.match(evento.request).then((emCache) => {
        if (emCache) return emCache;
        return fetch(evento.request).then((resposta) => {
          const copia = resposta.clone();
          caches.open(VERSAO).then((cache) => cache.put(evento.request, copia));
          return resposta;
        });
      })
    );
  }
});
