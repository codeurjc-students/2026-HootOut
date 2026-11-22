import { test, expect } from '../../fixtures.js'
import {
  channelUrl,
  conversationIdOf,
  createChannel,
  createTestServer,
  gotoConversation,
  sendMessage,
} from '../server/Common.js'
import { friendLink, makeFriends } from '../../friends/Common.js'

// Below the 520px breakpoint the chat views show one part at a time
const MOBILE_VIEWPORT = { width: 390, height: 800 }

test.use({ viewport: MOBILE_VIEWPORT })

test.describe('chat on mobile', () => {
  test('without a server only the server list and the left bar are shown', async ({ page }) => {
    await page.goto('/chat')

    await expect(page.locator('.chat-side-bar')).toBeVisible()
    await expect(page.locator('.chat-left-bar')).toBeVisible()
    await expect(page.locator('.chat-left-bar')).toContainText('Select a Server First')
    await expect(page.locator('.chat-conversation')).toBeHidden()
  })

  test('the channels and the chat are shown one at a time', async ({ page }) => {
    await page.goto('/chat')
    const server = await createTestServer(page)
    const channel = await createChannel(page, server)

    // Server: list of servers and channels, no chat
    await page.goto(`/chat/server/${server.id}`)
    await expect(page.locator('.chat-side-bar')).toBeVisible()
    await expect(page.locator('.chat-left-bar')).toBeVisible()
    await expect(page.locator('.chat-conversation')).toBeHidden()

    // Channel: only the chat
    await page.locator('.channel-name', { hasText: channel.name }).click()
    await expect(page).toHaveURL(new RegExp(`${channelUrl(channel)}$`))
    await expect(page.locator('.chat-side-bar')).toBeHidden()
    await expect(page.locator('.chat-left-bar')).toBeHidden()
    await expect(page.locator('.chat-conversation')).toBeVisible()
    await expect(page.locator('#message-input')).toBeVisible()

    // The back link of the chat goes back to the channels
    const backLink = page.locator('.chat-info').getByRole('link')
    await expect(backLink).toBeVisible()
    await expect(backLink).toContainText(channel.name)
    await backLink.click()

    await expect(page).toHaveURL(new RegExp(`/chat/server/${server.id}$`))
    await expect(page.locator('.chat-left-bar')).toBeVisible()
    await expect(page.locator('.channel-name', { hasText: channel.name })).toBeVisible()
    await expect(page.locator('.chat-conversation')).toBeHidden()
  })

  test('messages can be sent from a channel', async ({ page }) => {
    await page.goto('/chat')
    const server = await createTestServer(page)
    const channel = await createChannel(page, server)
    await gotoConversation(page, channelUrl(channel), conversationIdOf(channel))

    await sendMessage(page, 'sent from mobile')
    await expect(page.locator('.chat-message')).toHaveCount(1)
  })

  test('the friends list and the direct conversation are shown one at a time', async ({
    createUser,
  }) => {
    test.slow()
    const user1 = await createUser()
    const user2 = await createUser()
    // Pages of createUser get the default viewport of a new context
    await user1.page.setViewportSize(MOBILE_VIEWPORT)
    await makeFriends(user1, user2)

    // Direct messages: friends list, no chat
    await user1.page.goto('/chat/direct')
    const link = friendLink(user1.page, user2.account.username)
    await expect(link).toBeVisible()
    await expect(user1.page.locator('.chat-side-bar')).toBeVisible()
    await expect(user1.page.locator('.chat-conversation')).toBeHidden()

    // Conversation: only the chat, with the name of the friend
    await link.click()
    await expect(user1.page).toHaveURL(/\/chat\/direct\/.+/)
    await expect(user1.page.locator('.chat-side-bar')).toBeHidden()
    await expect(user1.page.locator('.chat-left-bar')).toBeHidden()
    await expect(user1.page.locator('.chat-conversation')).toBeVisible()

    const backLink = user1.page.locator('.chat-info').getByRole('link')
    await expect(backLink).toContainText(user2.account.username)
    await backLink.click()

    await expect(user1.page).toHaveURL(/\/chat\/direct$/)
    await expect(friendLink(user1.page, user2.account.username)).toBeVisible()
    await expect(user1.page.locator('.chat-conversation')).toBeHidden()
  })
})

test.describe('chat on desktop', () => {
  test.use({ viewport: { width: 1280, height: 720 } })

  test('the channels and the chat are shown together, without back link', async ({ page }) => {
    await page.goto('/chat')
    const server = await createTestServer(page)
    const channel = await createChannel(page, server)

    await page.goto(channelUrl(channel))
    await expect(page.locator('.chat-side-bar')).toBeVisible()
    await expect(page.locator('.chat-left-bar')).toBeVisible()
    await expect(page.locator('.chat-conversation')).toBeVisible()
    await expect(page.locator('.chat-info')).toContainText(channel.name)
    await expect(page.locator('.chat-info').getByRole('link')).toBeHidden()
  })
})
