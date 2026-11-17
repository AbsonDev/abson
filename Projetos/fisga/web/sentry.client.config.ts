import * as Sentry from '@sentry/astro';
import {
  limparEvento,
  limparEventoDeReplay,
  limparGravacao,
  taxaDeAmostragem,
  taxaDeReplay
} from './src/lib/sentry-scrub';

const dsn = import.meta.env.PUBLIC_SENTRY_DSN;
const ambiente = import.meta.env.PUBLIC_SENTRY_ENVIRONMENT ?? (import.meta.env.DEV ? 'development' : 'production');

if (dsn) {
  Sentry.init({
    dsn,
    environment: ambiente,
    release: import.meta.env.PUBLIC_SENTRY_RELEASE,
    integrations: [
      Sentry.browserTracingIntegration(),
      Sentry.replayIntegration({
        // O Fisga tem diário, fotos, coordenadas e dados de conta.
        maskAllText: true,
        maskAllInputs: true,
        blockAllMedia: true,
        block: [
          '.leaflet-container',
          '#mapa',
          '.captura-fotos',
          '.captura-foto-thumb',
          '.pescaria-card-foto',
          '.trofeu-foto',
          '[data-replay-block]'
        ],
        ignore: ['input[type="file"]'],
        beforeAddRecordingEvent: limparGravacao
      })
    ],
    tracesSampleRate: taxaDeAmostragem(),
    replaysSessionSampleRate: taxaDeReplay(),
    replaysOnErrorSampleRate: 1.0,
    dataCollection: {
      userInfo: false,
      cookies: false,
      httpHeaders: false,
      httpBodies: [],
      urlQueryParams: false,
      databaseQueryData: false,
      stackFrameVariables: false
    },
    beforeSend: limparEvento
  });

  Sentry.addEventProcessor((evento) => limparEventoDeReplay(evento));
}
