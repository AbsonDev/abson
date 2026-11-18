import { defineConfig, devices } from '@playwright/test';

/**
 * Suíte E2E com backend real (Task 13).
 *
 * Diferente de `playwright.config.ts`, NÃO sobe a API mock: exige a API real em
 * `E2E_API_URL` (padrão http://localhost:5184) com SQLite/outbox temporários.
 * Use `scripts/e2e-real.sh`, que prepara banco, chaves e outbox descartáveis.
 *
 *   ./scripts/e2e-real.sh
 *
 * O servidor web é reutilizado se já estiver em execução.
 */

const porta = Number(process.env.E2E_WEB_PORT ?? 4323);
const baseURL = process.env.E2E_WEB_URL ?? `http://localhost:${porta}`;

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
    command: `npm run dev -- --port ${porta}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000
  }
});
