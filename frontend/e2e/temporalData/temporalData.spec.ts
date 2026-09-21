import { test, expect } from '@playwright/test';

test('test temporalData user list', async ({ page }) => {
    await page.goto('/')
    const items = await page.locator('#user-list li');

    await expect(items).not.toHaveCount(0);
})

// test('test temporalData websocket', async ({ page }) => {
//     await page.goto('/')
//     await expect(page.locator('')).toHaveText('')
// })

