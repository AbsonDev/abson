const VERSAO = 'fisga-v1';
const CASCA = [
  '/',
  '/entrar',
  '/meu-diario',
  '/registrar',
  '/precos',
  '/offline',
  '/manifest.webmanifest',
  '/favicon.svg',
  '/icon-192.png',
  '/icon-512.png',
  '/og-fisga.png'
];

self.addEventListener('install', (evento) => {
  evento.waitUntil(
    caches.open(VERSAO)
      .then((cache) => cache.addAll(CASCA))
      .then(() => self.skipWaiting())
  );
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
        .catch(async () => (await caches.match(evento.request)) || caches.match('/offline'))
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
