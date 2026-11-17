import { defineConfig } from 'astro/config';
import node from '@astrojs/node';
import sentry from '@sentry/astro/integration';

const sentryAuthToken = process.env.SENTRY_AUTH_TOKEN;

export default defineConfig({
  output: 'server',
  adapter: node({ mode: 'standalone' }),
  integrations: [
    sentry({
      org: 'absondev',
      project: 'fisga-web',
      authToken: sentryAuthToken,
      sourcemaps: sentryAuthToken ? {} : { disable: true },
      telemetry: false
    })
  ],
  site: process.env.PUBLIC_SITE_URL ?? 'http://localhost:4323',
  server: {
    host: true,
    port: 4323
  },
  devToolbar: {
    enabled: false
  }
});
