import { defineConfig, devices } from '@playwright/test';

/**
 * Suíte E2E com backend real (Task 13).
 *
 * Diferente de `playwright.config.ts`, NÃO sobe a API mock e NÃO reutiliza um
 * dev server qualquer: inicia um servidor dedicado em 4324, apontando para a
 * API real em `E2E_API_URL` (padrão http://localhost:5184). Use
 * `scripts/e2e-real.sh`, que prepara banco, chaves, outbox e envs descartáveis.
 *
 *   ./scripts/e2e-real.sh
 */

const porta = Number(process.env.E2E_WEB_PORT ?? 4324);
const baseURL = process.env.E2E_WEB_URL ?? `http://localhost:${porta}`;
const api = process.env.E2E_API_URL ?? 'http://localhost:5184';

export default defineConfig({
  testDir: './tests',
  testMatch: ['**/identity-e2e.spec.ts'],
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  reporter: 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome']
  },
  webServer: {
    command: `npm run dev -- --port ${porta} --ignore-lock`,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 120_000,
    env: {
      PUBLIC_API_URL: api,
      PUBLIC_SITE_URL: baseURL,
      PUBLIC_SENTRY_DSN: '',
      SENTRY_DSN: ''
    }
  }
});
