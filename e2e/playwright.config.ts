import { defineConfig } from '@playwright/test'

// Runs against an already running stack: `docker compose up --build -d` (see README).
export default defineConfig({
  testDir: './tests',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    locale: 'bn-BD',
    trace: 'off',
  },
})
