import { test, expect } from '../fixtures.js';

// test('test temporalData user list', async ({ page }) => {
//     await page.goto('/temporalData');

//     page.on('console', msg => console.log('BROWSER:', msg.text()));
//     page.on('requestfailed', req => console.log('FAILED REQUEST:', req.url(), req.failure()?.errorText));

//     const items = page.locator('#user-list li');

//     await expect(items).not.toHaveCount(0);
// })

// // test('test temporalData websocket', async ({ page }) => {
// //     await page.goto('/temporalData');

// //     page.on('console', msg => console.log('BROWSER:', msg.text()));
// //     page.on('requestfailed', req => console.log('FAILED REQUEST:', req.url(), req.failure()?.errorText));

// //     await expect(page.locator('#wsstatus')).toHaveText('Connection Opened');

// //     let message = 'test message';
// //     await page.locator('#wsmessage-input').fill(message);

// //     await page.locator('#wsmessage-btn').click();

// //     await expect(page.locator('#wsmessage')).toHaveText(`HELLO FROM THE SERVER ${message.toUpperCase()}`);

// // })

test('test load home', async ({ page }) => {
    await page.goto('/');

    page.on('console', msg => console.log('BROWSER:', msg.text()));
    page.on('requestfailed', req => console.log('FAILED REQUEST:', req.url(), req.failure()?.errorText));

    const title = page.locator('h1');
    await expect(title).toHaveText("HootOut");

    const h2 = page.locator('h2');
    await expect(h2).toBeVisible();

    const image = page.locator('img');
    await expect(image).toHaveAttribute('src', '/jeremy-hynes-cE6r38Mj7gA-unsplash.jpg')
})
