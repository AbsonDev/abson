import * as Sentry from '@sentry/astro';
import { limparEvento, taxaDeAmostragem } from './src/lib/sentry-scrub';

const dsn = import.meta.env.PUBLIC_SENTRY_DSN;
const ambiente = import.meta.env.PUBLIC_SENTRY_ENVIRONMENT ?? (import.meta.env.DEV ? 'development' : 'production');

if (dsn) {
  Sentry.init({
    dsn,
    environment: ambiente,
    release: import.meta.env.PUBLIC_SENTRY_RELEASE,
    integrations: [Sentry.browserTracingIntegration()],
    tracesSampleRate: taxaDeAmostragem(),
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
}
