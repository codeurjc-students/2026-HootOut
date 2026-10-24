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

export const test = base.extend<{ autoCoverage: void }, { workerStorageState: string }>({
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
    // Use the same storage state for all tests in this worker.
    storageState: ({ workerStorageState }, use) => use(workerStorageState),

    // Authenticate once per worker with a worker-scoped fixture.
    workerStorageState: [async ({ browser }, use) => {
        // Use parallelIndex as a unique identifier for each worker.
        const id = test.info().parallelIndex;
        const fileName = path.resolve(test.info().project.outputDir, `.auth/${id}.json`);

        if (fs.existsSync(fileName)) {
            // Reuse existing authentication state if any.
            await use(fileName);
            return;
        }

        // Important: make sure we authenticate in a clean environment by unsetting storage state.
        const page = await browser.newPage({ storageState: undefined });
        const baseUrl = test.info().project.use.baseURL

        // Acquire a unique account, for example create a new one.
        // Alternatively, you can have a list of precreated accounts for testing.
        // Make sure that accounts are unique, so that multiple team members
        // can run tests at the same time without interference.
        const account = await acquireAccount(id);

        // Perform authentication steps. Replace these actions with your own.
        await page.goto(`${baseUrl}/register`);
        await page.getByLabel('Username').fill(account.username);
        await page.getByLabel('Email').fill(account.email);
        await page.getByLabel('Password', { exact: true }).fill(account.password);
        await page.getByLabel('Confirm Password', { exact: true }).fill(account.password);
        await page.getByRole('button', { name: 'Create Account' }).click();
        // Wait until the page receives the cookies.
        //
        // Sometimes login flow sets cookies in the process of several redirects.
        // Wait for the final URL to ensure that the cookies are actually set.
        await page.waitForURL(`${baseUrl}/chat`);
        // Alternatively, you can wait until the page reaches a state where all cookies are set.
        await expect(page.locator('.chat-left-bar')).toHaveText('Select a Server First');

        // End of authentication steps.

        await page.context().storageState({ path: fileName });
        await page.close();
        await use(fileName);
    }, { scope: 'worker' }],
});

export { expect } from '@playwright/test'