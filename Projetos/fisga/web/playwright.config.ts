import { defineConfig, devices } from '@playwright/test';

const porta = Number(process.env.PORT ?? 4323);
const baseURL = `http://localhost:${porta}`;

export default defineConfig({
  testDir: './tests',
  // A suíte real (`identity-e2e.spec.ts`) exige backend real e roda pelo
  // `playwright.real.config.ts`. A suíte padrão continua 100% mockada.
  testIgnore: ['**/identity-e2e.spec.ts'],
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome']
  },
  webServer: [
    {
      // `/hoje` chama a API no servidor; o mock precisa existir fora do navegador.
      command: 'node tests/mock-api.mjs',
      url: 'http://localhost:5184/__health',
      reuseExistingServer: !process.env.CI,
      timeout: 30_000
    },
    {
      // `astro dev` inicia um daemon e o processo pai encerra; o Playwright
      // interpreta isso como "exited early". Usar o servidor standalone do
      // build mantém o processo em foreground e é o mesmo artefato do deploy.
      command: 'npm run start',
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        PORT: String(porta),
        HOST: '127.0.0.1',
        API_URL: 'http://localhost:5184',
        PUBLIC_API_URL: 'http://localhost:5184',
        PUBLIC_SITE_URL: baseURL
      }
    }
  ]
});
