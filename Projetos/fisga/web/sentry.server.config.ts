import * as Sentry from '@sentry/astro';
import { limparEvento, taxaDeAmostragem } from './src/lib/sentry-scrub';

const dsn = process.env.SENTRY_DSN ?? import.meta.env.PUBLIC_SENTRY_DSN;
const ambiente =
  process.env.SENTRY_ENVIRONMENT ??
  import.meta.env.PUBLIC_SENTRY_ENVIRONMENT ??
  (import.meta.env.DEV ? 'development' : 'production');

if (dsn) {
  Sentry.init({
    dsn,
    environment: ambiente,
    release: process.env.SENTRY_RELEASE ?? import.meta.env.PUBLIC_SENTRY_RELEASE,
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
