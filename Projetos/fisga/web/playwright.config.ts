import { defineConfig, devices } from '@playwright/test';

const porta = Number(process.env.PORT ?? 4323);
const baseURL = `http://localhost:${porta}`;

export default defineConfig({
  testDir: './tests',
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
      command: `npm run dev -- --port ${porta}`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000
    }
  ]
});
