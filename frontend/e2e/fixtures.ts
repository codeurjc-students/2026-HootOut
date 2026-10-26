import process from 'node:process'
import fs from 'fs';
import path from 'path';
import { test as base, expect, type Browser, type TestInfo, type PlaywrightWorkerArgs } from '@playwright/test'
import { addCoverageReport } from 'monocart-reporter'
import { randomUUID } from 'node:crypto';

const distDir = path.resolve('dist')

export function acquireAccount(id: number = 0) {
    const uuid = randomUUID()
    const account = {
        username: `${id}playwright${uuid}`,
        email: `${uuid}@hootout.test`,
        password: uuid
    }
    return account;
}

function attachSourceMaps(entries: Array<{ url: string, sourceMap?: unknown }>) {
    for (const entry of entries) {
        let pathname: string
        try {
            pathname = new URL(entry.url).pathname
        }
        catch {
            continue
        }
        if (!pathname.endsWith('.js'))
            continue
        const mapFile = path.join(distDir, `${pathname.slice(1)}.map`)
        if (fs.existsSync(mapFile))
            entry.sourceMap = JSON.parse(fs.readFileSync(mapFile, 'utf-8'))
    }
}

async function register(browser: Browser, account: Account, testInfo: TestInfo, baseUrl: string) {
    const page = await browser.newPage({ ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors })
    try {
        await page.goto(`${baseUrl}/register`)
        await page.getByLabel('Username').fill(account.username)
        await page.getByLabel('Email').fill(account.email)
        await page.getByLabel('Password', { exact: true }).fill(account.password)
        await page.getByLabel('Confirm Password', { exact: true }).fill(account.password)
        await page.getByRole('button', { name: 'Create Account' }).click()
        await page.waitForURL(`${baseUrl}/chat`)
    } finally {
        await page.close()
    }
}

async function login(
    playwright: PlaywrightWorkerArgs['playwright'],
    account: Account,
    testInfo: TestInfo,
    name: string,
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

type Account = ReturnType<typeof acquireAccount>

export const test = base.extend<{ autoCoverage: void, secondUserState: string }, { workerAccount: Account; secondAccount: Account }>({
    autoCoverage: [
        async ({ page, browserName }, use, testInfo) => {
            const enabled = !!process.env.E2E_COVERAGE && browserName === 'chromium'

            if (enabled)
                await page.coverage.startJSCoverage({ resetOnNavigation: false })

            await use()

            if (enabled) {
                const coverage = await page.coverage.stopJSCoverage()
                attachSourceMaps(coverage)
                await addCoverageReport(coverage, testInfo)
            }
        },
        { auto: true },
    ],
    workerAccount: [async ({ browser }, use) => {
        const account = await acquireAccount(test.info().parallelIndex)
        await register(browser, account, test.info(), test.info().project.use.baseURL!)
        await use(account)
    }, { scope: 'worker' }],

    //  Only registered if used
    secondAccount: [async ({ browser }, use) => {
        const account = await acquireAccount(test.info().parallelIndex)
        await register(browser, account, test.info(), test.info().project.use.baseURL!)
        await use(account)
    }, { scope: 'worker' }],

    storageState: async ({ workerAccount, playwright }, use, testInfo) => {
        await use(await login(playwright, workerAccount, testInfo, 'user1'))
    },
    secondUserState: async ({ secondAccount, playwright }, use, testInfo) => {
        await use(await login(playwright, secondAccount, testInfo, 'user2'))
    },
});

export { expect } from '@playwright/test'