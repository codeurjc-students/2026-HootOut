import process from 'node:process'
import fs from 'fs'
import path from 'path'
import {
  test as base,
  expect,
  type Browser,
  type BrowserContext,
  type Page,
  type TestInfo,
  type PlaywrightWorkerArgs,
  type WorkerInfo,
} from '@playwright/test'
import { addCoverageReport } from 'monocart-reporter'
import { randomUUID } from 'node:crypto'

const distDir = path.resolve('dist')

export function acquireAccount(id: number = 0) {
  const uuid = randomUUID()
  const account = {
    username: `${id}playwright${uuid}`,
    email: `${uuid}@hootout.test`,
    password: uuid,
  }
  return account
}

function attachSourceMaps(entries: Array<{ url: string; sourceMap?: unknown }>) {
  for (const entry of entries) {
    let pathname: string
    try {
      pathname = new URL(entry.url).pathname
    } catch {
      continue
    }
    if (!pathname.endsWith('.js')) continue
    const mapFile = path.join(distDir, `${pathname.slice(1)}.map`)
    if (fs.existsSync(mapFile)) entry.sourceMap = JSON.parse(fs.readFileSync(mapFile, 'utf-8'))
  }
}

async function register(
  browser: Browser,
  account: Account,
  workerInfo: WorkerInfo,
  baseUrl: string
) {
  const page = await browser.newPage({
    ignoreHTTPSErrors: workerInfo.project.use.ignoreHTTPSErrors,
    storageState: { cookies: [], origins: [] },
  })
  try {
    await page.goto(`${baseUrl}/register`)
    await page.getByLabel('Username').fill(account.username)
    await page.getByLabel('Email').fill(account.email)
    await page.getByLabel('Password', { exact: true }).fill(account.password)
    await page.getByLabel('Confirm Password', { exact: true }).fill(account.password)
    await page.getByRole('button', { name: 'Create Account', exact: true }).click()
    await page.waitForURL(`${baseUrl}/chat`)
  } finally {
    await page.close()
  }
}

async function login(
  playwright: PlaywrightWorkerArgs['playwright'],
  account: Account,
  testInfo: TestInfo,
  name: string
) {
  const api = await playwright.request.newContext({
    baseURL: testInfo.project.use.baseURL,
    ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors,
    extraHTTPHeaders: { 'X-Token-Delivery': 'cookie' },
  })
  const res = await api.post('/api/v1/auth/login', {
    data: { email: account.email, password: account.password },
  })
  expect(res.ok(), `Failed Login (${name}): ${res.status()} ${await res.text()}`).toBeTruthy()
  const file = testInfo.outputPath(`auth-${name}.json`)
  await api.storageState({ path: file })
  await api.dispose()
  return file
}

export type Account = ReturnType<typeof acquireAccount>

export type NewUser = { account: Account; page: Page }

export const test = base.extend<
  {
    autoCoverage: void
    secondUserState: string
    secondPage: Page
    createUser: () => Promise<NewUser>
  },
  { workerAccount: Account; secondAccount: Account }
>({
  autoCoverage: [
    async ({ page, browserName }, use, testInfo) => {
      const enabled = !!process.env.E2E_COVERAGE && browserName === 'chromium'

      if (enabled) await page.coverage.startJSCoverage({ resetOnNavigation: false })

      await use()

      if (enabled) {
        const coverage = await page.coverage.stopJSCoverage()
        attachSourceMaps(coverage)
        await addCoverageReport(coverage, testInfo)
      }
    },
    { auto: true },
  ],
  workerAccount: [
    async ({ browser }, use, workerInfo) => {
      const account = acquireAccount(workerInfo.parallelIndex)
      await register(browser, account, workerInfo, workerInfo.project.use.baseURL!)
      await use(account)
    },
    { scope: 'worker' },
  ],

  //  Only registered if used
  secondAccount: [
    async ({ browser }, use, workerInfo) => {
      const account = acquireAccount(workerInfo.parallelIndex)
      await register(browser, account, workerInfo, workerInfo.project.use.baseURL!)
      await use(account)
    },
    { scope: 'worker' },
  ],

  storageState: async ({ workerAccount, playwright }, use, testInfo) => {
    await use(await login(playwright, workerAccount, testInfo, 'user1'))
  },
  secondUserState: async ({ secondAccount, playwright }, use, testInfo) => {
    await use(await login(playwright, secondAccount, testInfo, 'user2'))
  },

  // Page of the second user, in its own browser context. The context is closed after the test
  secondPage: async ({ browser, secondUserState }, use, testInfo) => {
    const context = await browser.newContext({
      storageState: secondUserState,
      ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors,
    })
    const page = await context.newPage()
    await use(page)
    await context.close()
  },

  // Registers a new user, only for this test, and opens a logged page for it.
  // For tests that change the state between two users (e.g. friendships), which cannot
  // be undone and would break the next tests of the worker
  createUser: async ({ browser, playwright }, use, testInfo) => {
    const contexts: BrowserContext[] = []
    let count = 0

    await use(async () => {
      const account = acquireAccount(testInfo.parallelIndex)
      const api = await playwright.request.newContext({
        baseURL: testInfo.project.use.baseURL,
        ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors,
      })
      const res = await api.post('/api/v1/auth/register', {
        data: { username: account.username, email: account.email, password: account.password },
      })
      expect(res.ok(), `Failed Register: ${res.status()} ${await res.text()}`).toBeTruthy()
      await api.dispose()

      const state = await login(playwright, account, testInfo, `new-user-${++count}`)
      const context = await browser.newContext({
        storageState: state,
        ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors,
      })
      contexts.push(context)
      return { account, page: await context.newPage() }
    })

    for (const context of contexts) await context.close()
  },
})

export { expect } from '@playwright/test'
