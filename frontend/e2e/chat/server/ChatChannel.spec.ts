import { test, expect } from '../../fixtures.js';

test('test create and enter channel', async ({ page }) => {
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

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}`));

    // Create Channel
    await page.locator(".add-channel").getByRole("button").click();

    await expect(page.locator('.modal')).toBeVisible();
    await expect(page.locator('.modal-title')).toContainText('Create New Channel');
    await expect(page.locator('#create-channel-form').locator(`[aria-label="${server.name}"]`)).toContainText(server.name);

    const submitChannelBtn = page.getByRole('button', { name: 'Create Channel' });
    await expect(submitChannelBtn).toBeDisabled();

    const createChannelPromise = page.waitForResponse(
        (res) => res.url().includes('/api/v1/chatchannels') && res.request().method() === 'POST'
    )

    const channel = {
        name: 'playwright-test',
    };

    await page.getByLabel('Channel Name').fill(channel.name);

    await expect(submitChannelBtn).toBeEnabled();
    await submitChannelBtn.click();

    const responseChannel = await createChannelPromise;
    expect(responseChannel.status()).toBe(200)
    const bodyChannel = await responseChannel.json();
    const channelId = bodyChannel.id;

    expect(channelId).toBeTruthy();
    expect(bodyChannel.name).toBe(channel.name);

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}/channel/${channelId}`));

    await expect(page.locator('.modal')).not.toBeVisible();

    await expect(page.locator('.channel-name-row.background-light').locator('.channel-name')).toHaveText(channel.name);
});

test('test create and update channel', async ({ page }) => {
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

    await expect(page.locator('.modal')).not.toBeVisible();

    const serverbtn = page.locator('#server-list').locator(`[server-id="${id}"]`)

    expect(serverbtn).not.toBeNull();

    await (serverbtn).click();

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}`));

    // Create Channel
    await page.locator(".add-channel").getByRole("button").click();

    await expect(page.locator('.modal')).toBeVisible();
    await expect(page.locator('#create-channel-form').locator(`[aria-label="${server.name}"]`)).toContainText(server.name);

    const submitChannelBtn = page.getByRole('button', { name: 'Create Channel' });
    await expect(submitChannelBtn).toBeDisabled();

    const createChannelPromise = page.waitForResponse(
        (res) => res.url().includes('/api/v1/chatchannels') && res.request().method() === 'POST'
    )

    const channel = {
        name: 'playwright-test',
    };

    await page.getByLabel('Channel Name').fill(channel.name);

    await expect(submitChannelBtn).toBeEnabled();
    await submitChannelBtn.click();

    const responseChannel = await createChannelPromise;
    expect(responseChannel.status()).toBe(200)
    const bodyChannel = await responseChannel.json();
    const channelId = bodyChannel.id;

    expect(channelId).toBeTruthy();
    expect(bodyChannel.name).toBe(channel.name);

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}/channel/${channelId}`));

    await expect(page.locator('.modal')).not.toBeVisible();

    //Edit channel
    await page.locator('.channel-name-row.background-light').getByRole('button').click();

    await expect(page.locator('.modal')).toBeVisible();
    await expect(page.locator('.modal-title')).toContainText('Update Channel');
    await expect(page.locator('#update-channel-form').locator(`[aria-label="${server.name}"]`)).toContainText(server.name);

    await expect(page.getByLabel('Channel Name')).toHaveValue(channel.name);
    const updatedChannel = {
        name: 'playwright-test-updated'
    }
    await page.getByLabel('Channel Name').fill(updatedChannel.name);

    const updateChannelBtn = page.getByRole('button', { name: 'Update Channel' });
    await expect(updateChannelBtn).toBeEnabled();

    const updateChannelResponse = page.waitForResponse(
        (res) => res.url().includes('/api/v1/chatchannels') && res.request().method() === 'PUT'
    )

    await updateChannelBtn.click();

    const updatedChannelResponse = await updateChannelResponse;
    expect(updatedChannelResponse.status()).toBe(200)
    const bodyUpdatedChannel = await updatedChannelResponse.json();
    const updatedChannelId = bodyUpdatedChannel.id;

    expect(channelId).toBe(updatedChannelId);
    expect(bodyUpdatedChannel.name).toBe(updatedChannel.name);

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}/channel/${channelId}`));
    await expect(page.locator('.modal')).not.toBeVisible();
});