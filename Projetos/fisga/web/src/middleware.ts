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
  // Páginas de autenticação são SSR (passam pelo middleware) e iguais para
  // todo visitante; o precache do service worker depende do header público.
  '/entrar',
  '/criar-conta',
  '/esqueci-senha',
  '/redefinir-senha',
  '/confirmar-email',
  '/entrar/confirmar',
  '/offline',
  '/newsletter/descadastrado',
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

interface PoliticaDeCache {
  cacheControl: string;
  variarPorCookie: boolean;
}

/**
 * Anexa `Cookie` ao `Vary` sem sobrescrever valores já definidos. Respostas
 * `/` e `/condicoes` mudam conforme a sessão: sem `Vary`, um cache intermediário
 * poderia servir o HTML anônimo a quem tem cookie (ou o contrário).
 */
function acrescentarVaryCookie(cabecalhos: Headers): void {
  const atual = cabecalhos.get('Vary');
  const valores = atual
    ? atual.split(',').map((valor) => valor.trim()).filter(Boolean)
    : [];

  if (valores.some((valor) => valor === '*' || valor.toLowerCase() === 'cookie')) {
    return;
  }

  cabecalhos.set('Vary', [...valores, 'Cookie'].join(', '));
}

function politicaDeCache(caminho: string, temCookie: boolean): PoliticaDeCache | null {
  const rota = normalizar(caminho);

  if (PRIVADAS.some((privada) => combina(rota, privada))) {
    return { cacheControl: NO_STORE, variarPorCookie: false };
  }

  // `/p/[token]` mostra `podeEditar` conforme o visitante; curto e privado.
  if (combina(rota, '/p')) {
    return { cacheControl: COMPARTILHADA, variarPorCookie: false };
  }

  if (ROTAS_ANONIMAS.includes(rota)) {
    return {
      cacheControl: temCookie ? NO_STORE : PAGINA_ANONIMA,
      variarPorCookie: !temCookie
    };
  }

  if (PUBLICAS_POR_MAIS_TEMPO.some((publica) => combina(rota, publica))) {
    return { cacheControl: CONTEUDO_PUBLICO, variarPorCookie: false };
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
  const politica = politicaDeCache(context.url.pathname, Boolean(cookie));

  if (politica) {
    resposta.headers.set('Cache-Control', politica.cacheControl);
    if (politica.variarPorCookie) {
      acrescentarVaryCookie(resposta.headers);
    }
  }

  return resposta;
});
