
import { Page } from '@playwright/test';
import { expect } from '../../fixtures.js';

export type Server = { id: string, name: string, description: string };
export type Channel = { id: string, serverId: string, name: string };

export async function createTestServer(page: Page): Promise<Server> {
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

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}`));
    return { id: id, name: server.name, description: server.description };
}

export async function createChannel(page: Page, server: Server): Promise<Channel> {
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

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}/channel/${channelId}`));

    await expect(page.locator('.modal')).not.toBeVisible();

    return { id: channelId, serverId: server.id, name: channel.name };
}