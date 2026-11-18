import type { ErrorEvent } from '@sentry/astro';

const CHAVES_SENSIVEIS = new Set([
  'lat',
  'lon',
  'latitude',
  'longitude',
  'nome',
  'cidade',
  'pescador',
  'email',
  'token',
  'uid',
  'handoff_code',
  'code',
  'q',
  'busca',
  'termo'
]);

// Allowlist explícita: qualquer chave fora dela é filtrada por padrão, para um
// parâmetro novo (ex.: termo de busca) não vazar antes de ser classificado.
const CHAVES_PUBLICAS = new Set([
  'page',
  'pagina',
  'per_page',
  'limite',
  'offset',
  'ordenar',
  'ordem',
  'ts',
  'v',
  'lang',
  'idioma',
  'utm_source',
  'utm_medium',
  'utm_campaign',
  'utm_content'
]);

const PARAMETRO_SENSIVEL =
  /([?&](?:lat|lon|latitude|longitude|nome|cidade|pescador|email|token|uid|handoff_code|code|q|busca|termo)=)[^&\s]+/gi;

function limparQuery(query: string): string {
  return query
    .replace(/^\?/, '')
    .split('&')
    .filter(Boolean)
    .map((parte) => {
      const igual = parte.indexOf('=');
      if (igual <= 0) return parte;

      const chave = decodeURIComponent(parte.slice(0, igual));
      const filtrar = CHAVES_SENSIVEIS.has(chave) || !CHAVES_PUBLICAS.has(chave);
      return filtrar ? `${parte.slice(0, igual)}=[Filtered]` : parte;
    })
    .join('&');
}

export function limparUrl(url: string): string {
  const separador = url.indexOf('?');
  if (separador < 0) return url;
  return `${url.slice(0, separador)}?${limparQuery(url.slice(separador + 1))}`;
}

export function limparTexto(texto: string): string {
  return texto.replace(PARAMETRO_SENSIVEL, '$1[Filtered]');
}

/**
 * Remove dados do diário, coordenadas e identificadores antes de enviar ao Sentry.
 */
export function limparEvento(evento: ErrorEvent): ErrorEvent {
  evento.user = undefined;

  if (evento.request) {
    delete evento.request.cookies;
    delete evento.request.data;

    if (typeof evento.request.query_string === 'string') {
      evento.request.query_string = limparQuery(evento.request.query_string);
    }

    if (typeof evento.request.url === 'string') {
      evento.request.url = limparUrl(evento.request.url);
    }
  }

  return evento;
}

/**
 * Limpa URLs de eventos de replay (navegação, requisições e console).
 */
export function limparEventoDeReplay(evento: any): any {
  if (evento?.type !== 'replay_event') return evento;

  if (Array.isArray(evento.urls)) {
    evento.urls = evento.urls.map((url: string) => limparUrl(url));
  }

  return evento;
}

/**
 * Limpa URLs e mensagens antes de um evento de gravação sair do navegador.
 * O conteúdo de texto, inputs e mídia já é mascarado/bloqueado pelo Replay.
 */
export function limparGravacao(evento: any): any {
  const payload = evento?.data?.payload;

  if (payload && typeof payload === 'object') {
    if (typeof payload.url === 'string') payload.url = limparUrl(payload.url);
    if (typeof payload.name === 'string') payload.name = limparTexto(payload.name);
    if (typeof payload.message === 'string') payload.message = limparTexto(payload.message);
  }

  if (typeof evento?.data?.message === 'string') {
    evento.data.message = limparTexto(evento.data.message);
  }

  return evento;
}

export function taxaDeAmostragem(): number {
  const configurada = import.meta.env.PUBLIC_SENTRY_TRACES_SAMPLE_RATE;
  if (configurada) {
    const valor = Number(configurada);
    if (Number.isFinite(valor)) return Math.min(1, Math.max(0, valor));
  }

  return import.meta.env.DEV ? 1.0 : 0.1;
}

export function taxaDeReplay(): number {
  const configurada = import.meta.env.PUBLIC_SENTRY_REPLAYS_SESSION_SAMPLE_RATE;
  if (configurada) {
    const valor = Number(configurada);
    if (Number.isFinite(valor)) return Math.min(1, Math.max(0, valor));
  }

  return import.meta.env.DEV ? 1.0 : 0.1;
}
