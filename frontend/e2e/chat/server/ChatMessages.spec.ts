import { test, expect } from '../../fixtures.js';
import { createChannel, createTestServer } from './Common.js';

test('test send messages in a channel', async ({ page: user1, browser, secondUserState }) => {

    const user2Context = await browser.newContext({ storageState: secondUserState })
    const user2 = await user2Context.newPage()

    await user1.goto('/chat');

    const server = await createTestServer(user1);

    await expect(user1).toHaveURL(new RegExp(`.*/chat/server/${server.id}`));

    // Create Channel
    const channel = await createChannel(user1, server);


    await user2.goto(`/chat/server/${server.id}/channel/${channel.id}`);
    const wsPromise = user2.waitForEvent('websocket', ws => ws.url().includes('/ws'));

    const ws = await wsPromise;
    const framePromise = ws.waitForEvent('framesent', {
        predicate: (event) => {
            try {
                const data = JSON.parse(event.payload.toString());
                return data.type == 1 && data.channel == channel.id;
            } catch {
                return false;
            }
        },
        timeout: 3000
    });

    expect(framePromise).toBeTruthy();

    await expect(user2.locator('.chat-info')).toContainText(channel.name);

    await expect(user1.locator('.channel-name-row.background-light').locator('.channel-name')).toHaveText(channel.name);

    await expect(user1.locator('.chat-info')).toContainText(channel.name);

    await expect(user1.locator('#messages-list > *')).toHaveCount(0);
    await expect(user2.locator('#messages-list > *')).toHaveCount(0);

    const message1 = "this is a test"
    await user1.locator('#message-input').fill(message1);
    await user1.locator('#wsmessage-btn').click();

    await expect(user1.locator('#messages-list > *')).toHaveCount(1);
    await expect(user2.locator('#messages-list > *')).toHaveCount(1);

    await expect(user1.locator('#messages-list > *')).toContainText(message1);
    await expect(user2.locator('#messages-list > *')).toContainText(message1);

    await expect(user1.locator('#messages-list > .background-soft')).toHaveCount(1);
    await expect(user2.locator('#messages-list > .background-soft')).toHaveCount(0);
});
