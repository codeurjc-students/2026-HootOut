import process from 'node:process'
import fs from 'fs';
import path from 'path';
import { test as base, expect } from '@playwright/test'
import { addCoverageReport } from 'monocart-reporter'
import { randomUUIDv7 } from 'node:crypto';

const distDir = path.resolve('dist')

export function acquireAccount(id: number = 0) {
    const uuid = randomUUIDv7()
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

type Account = Awaited<ReturnType<typeof acquireAccount>>

export const test = base.extend<{ autoCoverage: void }, { workerAccount: Account }>({
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
        const id = test.info().parallelIndex
        const account = acquireAccount(id)
        const baseUrl = test.info().project.use.baseURL

        const page = await browser.newPage({ storageState: undefined })
        await page.goto(`${baseUrl}/register`)
        await page.getByLabel('Username').fill(account.username)
        await page.getByLabel('Email').fill(account.email)
        await page.getByLabel('Password', { exact: true }).fill(account.password)
        await page.getByLabel('Confirm Password', { exact: true }).fill(account.password)
        await page.getByRole('button', { name: 'Create Account' }).click()
        await page.waitForURL(`${baseUrl}/chat`)
        await expect(page.locator('.chat-left-bar')).toHaveText('Select a Server First');
        await page.close()

        await use(account)
    }, { scope: 'worker' }],
    //A new session for each test, with a call to the login api
    storageState: async ({ workerAccount, playwright }, use, testInfo) => {
        const api = await playwright.request.newContext({ baseURL: testInfo.project.use.baseURL, ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors })
        const res = await api.post('/api/v1/auth/login', {
            headers: { 'X-Token-Delivery': 'cookie' },
            data: { email: workerAccount.email, password: workerAccount.password },
        })
        expect(res.ok(), `Failed Login: ${res.status()} ${await res.text()}`).toBeTruthy()

        const file = testInfo.outputPath('auth.json')
        await api.storageState({ path: file })
        await api.dispose()
        await use(file)
    },
});

export { expect } from '@playwright/test'