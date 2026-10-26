import { test, expect } from '../../fixtures.js';

test('test close server modal', async ({ page }) => {
    await page.goto('/chat');
    await page.locator('#new-server-btn').click();

    const modal = page.locator('.modal')
    await expect(modal).toBeVisible();
    await expect(page.locator('.modal-title')).toContainText('Create New Server');

    const closeBtn = page.locator('.modal-close');
    await closeBtn.click();

    await expect(modal).not.toBeVisible();
});

test('test create and enter server', async ({ page }) => {
    await page.goto('/chat');

    await page.locator('#new-server-btn').click();

    await expect(page.locator('.modal')).toBeVisible();
    await expect(page.locator('.modal-title')).toContainText('Create New Server');

    const submitBtn = page.getByRole('button', { name: 'Create Server' });
    await expect(submitBtn).toBeDisabled();

    const responsePromise = page.waitForResponse(
        (res) => res.url().includes('/api/v1/chatServers') && res.request().method() === 'POST'
    )

    const server = {
        name: 'playwright-test',
        description: 'test'
    };

    await page.getByLabel('Server Name').fill(server.name);
    await page.getByLabel('Server Description', { exact: true }).fill(server.description);

    await expect(submitBtn).toBeEnabled();
    await submitBtn.click();

    const response = await responsePromise
    expect(response.status()).toBe(200)
    const body = await response.json();
    const id = body.id;

    expect(id).toBeTruthy();
    expect(body.name).toBe(server.name);
    expect(body.description).toBe(server.description);

    await expect(page.locator('.modal')).not.toBeVisible();

    const serverList = page.locator('#server-list');
    const serverbtn = serverList.locator(`[server-id="${id}"]`)

    expect(serverbtn).not.toBeNull();

    await (serverbtn).click();

    await expect(page).toHaveURL(`/chat/server/${id}`);
    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}`));
});







