import { test, expect } from '../fixtures.js';

test('test load home', async ({ page }) => {
    await page.goto('/');

    const title = page.locator('h1');
    await expect(title).toHaveText("HootOut");

    const h2 = page.locator('h2');
    await expect(h2).toBeVisible();

    const image = page.locator('#home-image>img');
    await expect(image).toHaveAttribute('src', '/jeremy-hynes-cE6r38Mj7gA-unsplash.jpg')
})
