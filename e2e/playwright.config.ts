import { defineConfig } from '@playwright/test';

// Se ejecuta contra el entorno de docker-compose (web en el puerto 4392). Una sola persona a la vez: comparten la base.
export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  workers: 1,
  fullyParallel: false,
  reporter: [['list']],
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:4392',
    viewport: { width: 1366, height: 800 },
    locale: 'es-SV',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  outputDir: './resultados',
});
