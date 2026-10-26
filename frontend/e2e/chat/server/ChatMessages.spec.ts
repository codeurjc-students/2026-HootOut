import { test, expect } from '../../fixtures.js';
import { createChannel, createTestServer } from './Common.js';

test('test send messages in a channel', async ({ page }) => {
    await page.goto('/chat');

    await page.goto('/chat');

    const server = await createTestServer(page);

    await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}`));

    // Create Channel
    const channel = await createChannel(page, server);

    await expect(page.locator('.channel-name-row.background-light').locator('.channel-name')).toHaveText(channel.name);
});
