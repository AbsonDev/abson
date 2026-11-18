import { defineMiddleware } from 'astro:middleware';

/**
 * Política de cache do HTML no servidor Astro.
 *
 * A API já responde com default-deny; o HTML precisa do mesmo cuidado porque
 * um proxy/CDN ou o próprio navegador poderia guardar `/hoje`, `/conta` ou a
 * versão autenticada de `/condicoes`. A regra vale por rota e nunca sobrescreve
 * um `Cache-Control` já definido pela página ou pelo adapter.
 */
const PRIVADAS = [
  '/hoje',
  '/mapa',
  '/conta',
  '/meu-diario',
  '/registrar',
  '/trofeu',
  '/aceite',
  '/boas-vindas',
  '/locais'
];

const PUBLICAS_POR_MAIS_TEMPO = [
  '/wiki',
  '/sobre',
  '/termos',
  '/privacidade',
  '/contato',
  '/precos',
  '/aprender',
  '/ferramentas'
];

/** Rotas cujo HTML muda com a sessão e, sem cookie, são shell público. */
const ROTAS_ANONIMAS = ['/', '/condicoes'];

const NO_STORE = 'private, no-store';
const PAGINA_ANONIMA = 'public, max-age=60, s-maxage=300, stale-while-revalidate=600';
const CONTEUDO_PUBLICO = 'public, max-age=3600, stale-while-revalidate=86400';
const COMPARTILHADA = 'private, max-age=30';

function combina(caminho: string, rota: string): boolean {
  return caminho === rota || caminho.startsWith(`${rota}/`);
}

/**
 * O Astro usa `trailingSlash: 'ignore'`: `/condicoes` e `/condicoes/` servem a
 * mesma página. Normalizar evita que a variante com barra escape da política e
 * saia sem `Cache-Control`, inclusive quando autenticada.
 */
function normalizar(caminho: string): string {
  return caminho.length > 1 && caminho.endsWith('/') ? caminho.slice(0, -1) : caminho;
}

function politicaDeCache(caminho: string, temCookie: boolean): string | null {
  const rota = normalizar(caminho);

  if (PRIVADAS.some((privada) => combina(rota, privada))) {
    return NO_STORE;
  }

  // `/p/[token]` mostra `podeEditar` conforme o visitante; curto e privado.
  if (combina(rota, '/p')) {
    return COMPARTILHADA;
  }

  if (ROTAS_ANONIMAS.includes(rota)) {
    return temCookie ? NO_STORE : PAGINA_ANONIMA;
  }

  if (PUBLICAS_POR_MAIS_TEMPO.some((publica) => combina(rota, publica))) {
    return CONTEUDO_PUBLICO;
  }

  return null;
}

export const onRequest = defineMiddleware(async (context, next) => {
  const resposta = await next();

  // Erro nunca herda TTL positivo: um 404/429/500 com max-age ficaria pinado
  // no navegador ou no CDN. A regra vale mesmo quando a página definiu o header.
  if (resposta.status >= 400) {
    resposta.headers.set('Cache-Control', NO_STORE);
    return resposta;
  }

  if (resposta.headers.has('Cache-Control')) {
    return resposta;
  }

  const cookie = context.request.headers.get('cookie');
  const valor = politicaDeCache(context.url.pathname, Boolean(cookie));

  if (valor) {
    resposta.headers.set('Cache-Control', valor);
  }

  return resposta;
});
