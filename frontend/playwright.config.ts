import process from 'node:process'
import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'

const coverageReporter = [
  'monocart-reporter',
  {
    name: 'E2E',
    outputFile: './monocart-report/index.html',
    coverage: {
      outputDir: path.resolve('coverage/e2e'),
      reports: ['v8', 'lcovonly'],
      entryFilter: (entry: { url: string }) =>
        entry.url.startsWith('https://localhost:5173')
        && !entry.url.includes('/node_modules/')
        && !entry.url.includes('/@vite/')
        && !entry.url.includes('/@id/')
        && !entry.url.includes('type=style')
        && !entry.url.endsWith('.css'),
      sourceFilter: (p: string) => {
        const path = p.replace(/\\/g, '/')
        return /(^|\/)src\/.+\.(vue|ts|js)$/.test(path) && !path.includes('node_modules')
      },
      sourcePath: (p: string) => p.replace(/\\/g, '/').replace(/^.*?(?=src\/)/, ''),
    },
  },
] as const

/**
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  testDir: './e2e',
  /* Maximum time one test can run for. */
  timeout: 30 * 1000,
  expect: {
    /**
     * Maximum time expect() should wait for the condition to be met.
     * For example in `await expect(locator).toHaveText();`
     */
    timeout: 3000,
  },
  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,
  /* Retry on CI only */
  retries: process.env.CI ? 1 : 0,
  /* Opt out of parallel tests on CI. */
  workers: process.env.CI ? 1 : undefined,
  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: process.env.E2E_COVERAGE
    ? [['list'], ['html'], coverageReporter]
    : 'html',
  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    /* Maximum time each action such as `click()` can take. Defaults to 0 (no limit). */
    actionTimeout: 0,
    /* Base URL to use in actions like `await page.goto('/')`. */
    baseURL: 'https://localhost:5173',

    /* Collect trace when retrying the failed test. See https://playwright.dev/docs/trace-viewer */
    trace: 'on-first-retry',

    headless: true,

    ignoreHTTPSErrors: true,
  },

  /* Configure projects for major browsers */
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
      },
    },
    {
      name: 'firefox',
      use: {
        ...devices['Desktop Firefox'],
      },
    } //,
    // {
    //   name: 'webkit',
    //   use: {
    //     ...devices['Desktop Safari'],
    //   },
    // },

    /* Test against mobile viewports. */
    // {
    //   name: 'Mobile Chrome',
    //   use: {
    //     ...devices['Pixel 5'],
    //   },
    // },
    // {
    //   name: 'Mobile Safari',
    //   use: {
    //     ...devices['iPhone 12'],
    //   },
    // },

    /* Test against branded browsers. */
    // {
    //   name: 'Microsoft Edge',
    //   use: {
    //     channel: 'msedge',
    //   },
    // },
    // {
    //   name: 'Google Chrome',
    //   use: {
    //     channel: 'chrome',
    //   },
    // },
  ],

  /* Folder for test artifacts such as screenshots, videos, traces, etc. */
  // outputDir: 'test-results/',

  /* Run your local dev server before starting the tests */
  webServer: {
    /**
     * Use the dev server by default for faster feedback loop.
     * Use the preview server on CI for more realistic testing.
     * Playwright will re-use the local server if there is already a dev-server running.
     */
    command: process.env.CI ? 'npm run preview -- --mode test' : 'npm run dev -- --mode test',
    url: 'https://localhost:5173',
    ignoreHTTPSErrors: true,
    reuseExistingServer: false,
    stdout: 'pipe',
    stderr: 'pipe',
  },
})
