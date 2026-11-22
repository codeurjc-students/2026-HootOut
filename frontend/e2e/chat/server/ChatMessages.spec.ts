import { type Page } from '@playwright/test'
import { test, expect } from '../../fixtures.js'
import {
  attachFile,
  channelUrl,
  conversationIdOf,
  createChannel,
  createTestServer,
  gotoConversation,
  MESSAGES_PAGE_SIZE,
  sendMessage,
  setupChannelWithTwoUsers,
  type Channel,
} from './Common.js'

const fileName = 'png-test-32x32.png'

// One user with a new server and channel, subscribed to the channel
async function setupChannel(page: Page): Promise<Channel> {
  await page.goto('/chat')
  const server = await createTestServer(page)
  const channel = await createChannel(page, server)
  await gotoConversation(page, channelUrl(channel), conversationIdOf(channel))
  return channel
}

test('test send messages in a channel', async ({ page: user1, secondPage: user2, secondAccount }) => {
  await setupChannelWithTwoUsers(user1, user2, secondAccount.username)

  await expect(user1.locator('.chat-message')).toHaveCount(0)
  await expect(user2.locator('.chat-message')).toHaveCount(0)

  const message1 = 'this is a test'
  await user1.locator('#message-input').fill(message1)
  await user1.getByRole('button', { name: 'Send message', exact: true }).click()

  // The message is confirmed by the ack: it is no longer pending
  await expect(user1.locator('.chat-message')).toHaveCount(1)
  await expect(user1.locator('.pending-message')).toHaveCount(0)
  await expect(user2.locator('.chat-message')).toHaveCount(1)

  await expect(user1.locator('.chat-message')).toContainText(message1)
  await expect(user2.locator('.chat-message')).toContainText(message1)

  // Only the author sees the message highlighted
  await expect(user1.locator('.chat-message.background-soft')).toHaveCount(1)
  await expect(user2.locator('.chat-message.background-soft')).toHaveCount(0)
})

test('test send message with attachments', async ({
  page: user1,
  secondPage: user2,
  secondAccount,
}) => {
  await setupChannelWithTwoUsers(user1, user2, secondAccount.username)

  await expect(user1.locator('.chat-message')).toHaveCount(0)
  await expect(user2.locator('.chat-message')).toHaveCount(0)

  const message1 = 'this is a test'
  await user1.locator('#message-input').fill(message1)

  const uploadImagePromise = user1.waitForResponse(
    (res) => res.url().includes('/api/v1/files') && res.request().method() === 'POST'
  )

  await attachFile(user1, fileName, false)

  const uploadImageResponse = await uploadImagePromise
  expect(uploadImageResponse.status()).toBe(200)
  const image = await uploadImageResponse.json()
  const imageId = image.id
  expect(imageId).toBeTruthy()
  expect(image.fileName).toBe(fileName)
  expect(image.downloadUrl).toBeTruthy()
  expect(image.url).toBeTruthy()
  expect(image.contentType).toBe('image/png')

  await expect(user1.locator('.file-preview-entry > .file-preview-name')).toHaveText(fileName)
  await expect(user1.locator('.file-preview-entry > img')).toHaveAttribute('src', image.url)

  await user1.getByRole('button', { name: 'Send message', exact: true }).click()

  await expect(user1.locator('.chat-message')).toHaveCount(1)
  await expect(user1.locator('.pending-message')).toHaveCount(0)
  await expect(user2.locator('.chat-message')).toHaveCount(1)

  await expect(user1.locator('.chat-message')).toContainText(message1)
  await expect(user2.locator('.chat-message')).toContainText(message1)

  await expect(user1.locator('.chat-message.background-soft')).toHaveCount(1)
  await expect(user2.locator('.chat-message.background-soft')).toHaveCount(0)

  // The preview of the message bar is emptied after sending: only the one of the message is left
  await expect(user1.locator('.file-preview-entry')).toHaveCount(1)

  // User 1 chat
  const user1Attachment = user1.locator('#messages-list .file-preview-entry')
  await expect(user1Attachment.locator('.file-preview-name')).toHaveText(fileName)
  await expect(user1Attachment.locator('img')).toHaveAttribute('src', /.+/)

  // User 2 chat
  const user2Attachment = user2.locator('#messages-list .file-preview-entry')
  await expect(user2Attachment.locator('.file-preview-name')).toHaveText(fileName)
  await expect(user2Attachment.locator('img')).toHaveAttribute('src', /.+/)
})

test('messages are kept after reloading, newest first', async ({
  page: user1,
  secondPage: user2,
  secondAccount,
}) => {
  await setupChannelWithTwoUsers(user1, user2, secondAccount.username)

  await sendMessage(user1, 'first message')
  await sendMessage(user1, 'second message')
  await sendMessage(user1, 'third message')

  await user2.reload()
  const messages = user2.locator('.chat-message')
  await expect(messages).toHaveCount(3)
  // The list uses flex-col-reverse: the first element in the DOM is the newest message
  await expect(messages.nth(0)).toContainText('third message')
  await expect(messages.nth(1)).toContainText('second message')
  await expect(messages.nth(2)).toContainText('first message')
})

test('messages are only received in their channel', async ({
  page: user1,
  secondPage: user2,
  secondAccount,
}) => {
  // Two users, two channels and several navigations
  test.slow()
  const { server, channel: channelA } = await setupChannelWithTwoUsers(
    user1,
    user2,
    secondAccount.username
  )
  const channelB = await createChannel(user1, server)

  // user2 opens channel B and user1 writes in channel A
  await gotoConversation(user2, channelUrl(channelB), conversationIdOf(channelB))
  await gotoConversation(user1, channelUrl(channelA), conversationIdOf(channelA))
  await sendMessage(user1, 'message in channel A')

  // Then in channel B: when it arrives, the one of channel A would have arrived before
  await gotoConversation(user1, channelUrl(channelB), conversationIdOf(channelB))
  await sendMessage(user1, 'message in channel B')

  await expect(user2.locator('.chat-message').filter({ hasText: 'message in channel B' })).toBeVisible()
  await expect(user2.locator('.chat-message')).toHaveCount(1)

  // The message of channel A is in channel A
  await user2.locator('.channel-name', { hasText: channelA.name }).click()
  await expect(user2.locator('.chat-message').filter({ hasText: 'message in channel A' })).toBeVisible()
  await expect(user2.locator('.chat-message')).toHaveCount(1)
})

test('a message is sent with Enter and only when it has content', async ({ page }) => {
  await setupChannel(page)

  const input = page.locator('#message-input')
  const sendBtn = page.getByRole('button', { name: 'Send message', exact: true })

  await expect(sendBtn).toBeDisabled()
  await input.fill('   ')
  await expect(sendBtn).toBeDisabled()

  await input.fill('sent with enter')
  await expect(sendBtn).toBeEnabled()
  await input.press('Enter')

  await expect(input).toHaveValue('')
  await expect(page.locator('.chat-message')).toHaveCount(1)
  await expect(page.locator('.chat-message')).toContainText('sent with enter')
  await expect(page.locator('.pending-message')).toHaveCount(0)
  await expect(sendBtn).toBeDisabled()

  // Enter with an empty input sends nothing
  await input.press('Enter')
  await expect(page.locator('.pending-message')).toHaveCount(0)
  await expect(page.locator('.chat-message')).toHaveCount(1)
})

test('a message is pending until the ack arrives and goes back to the input if it fails', async ({
  page,
}) => {
  // The page talks to the backend through this route: chat messages can be dropped,
  // so no ack arrives (the app gives up after 5 seconds)
  let dropChatMessages = false
  let conversationId = ''
  let onSubscribed: () => void = () => {}

  await page.routeWebSocket(/\/ws/, (ws) => {
    const server = ws.connectToServer()
    ws.onMessage((message) => {
      let data: { type?: number; channel?: string; payload?: string } | null = null
      try {
        data = JSON.parse(message.toString())
      } catch {
        // Not a JSON frame
      }
      if (data?.type == 1 && data.channel == conversationId) onSubscribed()
      if (dropChatMessages && data?.payload) return
      server.send(message)
    })
  })

  await page.goto('/chat')
  const server = await createTestServer(page)
  const channel = await createChannel(page, server)
  conversationId = conversationIdOf(channel)
  const subscribed = new Promise<void>((resolve) => (onSubscribed = resolve))
  await page.goto(channelUrl(channel))
  await subscribed

  dropChatMessages = true
  const input = page.locator('#message-input')
  await input.fill('lost message')
  await page.getByRole('button', { name: 'Send message', exact: true }).click()

  // The input is emptied at once and the message is shown as pending
  await expect(input).toHaveValue('')
  const pending = page.locator('.pending-message')
  await expect(pending).toHaveCount(1)
  await expect(pending).toContainText('lost message')
  await expect(pending).toHaveClass(/opacity-25/)
  await expect(page.locator('.chat-message')).toHaveCount(0)

  // No ack: the pending message disappears and the text goes back to the input
  await expect(pending).toHaveCount(0, { timeout: 10_000 })
  await expect(input).toHaveValue('lost message')
  await expect(page.locator('.chat-message')).toHaveCount(0)

  // It can be sent again
  dropChatMessages = false
  await page.getByRole('button', { name: 'Send message', exact: true }).click()
  await expect(page.locator('.chat-message')).toHaveCount(1)
  await expect(page.locator('.chat-message')).toContainText('lost message')
  await expect(pending).toHaveCount(0)
})

test('a message cannot be sent while a file is uploading', async ({ page }) => {
  await setupChannel(page)

  // The upload waits until the test lets it continue
  let releaseUpload: () => void = () => {}
  const uploadReleased = new Promise<void>((resolve) => (releaseUpload = resolve))
  await page.route('**/api/v1/files**', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback()
    await uploadReleased
    await route.continue()
  })

  await page.locator('#message-input').fill('message with a file')
  await attachFile(page, fileName, false)

  const sendBtn = page.getByRole('button', { name: 'Send message', exact: true })
  const attachBtn = page.getByRole('button', { name: 'Attach file', exact: true })
  await expect(sendBtn).toBeDisabled()
  await expect(attachBtn).toBeDisabled()

  // Enter does not send either
  await page.locator('#message-input').press('Enter')
  await expect(page.locator('.pending-message')).toHaveCount(0)
  await expect(page.locator('#message-input')).toHaveValue('message with a file')

  releaseUpload()
  await expect(page.locator('.file-preview-entry .file-preview-name')).toHaveText(fileName)
  await expect(sendBtn).toBeEnabled()
  await expect(attachBtn).toBeEnabled()

  await sendBtn.click()
  await expect(page.locator('.chat-message')).toHaveCount(1)
  await expect(page.locator('#messages-list .file-preview-entry')).toHaveCount(1)
})

test('an attachment can be removed before sending', async ({ page }) => {
  await setupChannel(page)

  await attachFile(page, fileName)
  const preview = page.locator('.file-preview-entry')
  await expect(preview).toHaveCount(1)

  // Only a file: it can be sent without text
  const sendBtn = page.getByRole('button', { name: 'Send message', exact: true })
  await expect(sendBtn).toBeEnabled()

  await preview.locator('.file-preview-close-btn').click()
  await expect(preview).toHaveCount(0)
  await expect(sendBtn).toBeDisabled()
})

test('a message with only an attachment can be sent and opened', async ({ page }) => {
  await setupChannel(page)

  await attachFile(page, fileName)
  await expect(page.locator('.file-preview-entry')).toHaveCount(1)
  await page.getByRole('button', { name: 'Send message', exact: true }).click()

  const attachment = page.locator('#messages-list .file-preview-entry')
  await expect(attachment).toHaveCount(1)
  await expect(page.locator('.pending-message')).toHaveCount(0)

  // Opening it shows the file in a modal, with the download link
  await attachment.click()
  const modal = page.locator('.modal')
  await expect(modal).toBeVisible()
  await expect(modal.locator('.modal-title')).toContainText(fileName)
  await expect(modal.locator('img')).toHaveAttribute('src', /.+/)
  await expect(modal.locator('a.file-download')).toHaveAttribute('href', /.+/)

  await modal.locator('.modal-close').click()
  await expect(modal).not.toBeVisible()
})

test('older messages are loaded with the button', async ({ page }) => {
  test.slow()
  await setupChannel(page)

  // One more message than a page. Zero padded, so no text contains another one
  const total = MESSAGES_PAGE_SIZE + 1
  const text = (n: number) => `message ${String(n).padStart(3, '0')}`
  for (let n = 1; n <= total; n++) {
    await sendMessage(page, text(n))
  }

  await page.reload()
  const messages = page.locator('.chat-message')
  const olderBtn = page.getByRole('button', { name: 'Get Older Messages', exact: true })

  await expect(messages).toHaveCount(MESSAGES_PAGE_SIZE)
  await expect(messages.first()).toContainText(text(total))
  await expect(olderBtn).toBeVisible()

  await olderBtn.click()
  await expect(messages).toHaveCount(total)
  await expect(messages.last()).toContainText(text(1))
  // No more pages
  await expect(olderBtn).toHaveCount(0)
})
