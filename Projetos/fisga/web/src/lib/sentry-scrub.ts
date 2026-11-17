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
  'handoff_code',
  'code'
]);

const PARAMETRO_SENSIVEL =
  /([?&](?:lat|lon|latitude|longitude|nome|cidade|pescador|email|token|handoff_code|code)=)[^&\s]+/gi;

function limparQuery(query: string): string {
  return query
    .replace(/^\?/, '')
    .split('&')
    .filter(Boolean)
    .map((parte) => {
      const igual = parte.indexOf('=');
      if (igual <= 0) return parte;

      const chave = decodeURIComponent(parte.slice(0, igual));
      return CHAVES_SENSIVEIS.has(chave) ? `${parte.slice(0, igual)}=[Filtered]` : parte;
    })
    .join('&');
}

function limparUrl(url: string): string {
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

export function taxaDeAmostragem(): number {
  const configurada = import.meta.env.PUBLIC_SENTRY_TRACES_SAMPLE_RATE;
  if (configurada) {
    const valor = Number(configurada);
    if (Number.isFinite(valor)) return Math.min(1, Math.max(0, valor));
  }

  return import.meta.env.DEV ? 1.0 : 0.1;
}
