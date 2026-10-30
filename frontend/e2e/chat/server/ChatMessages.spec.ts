import { test, expect } from '../../fixtures.js'
import { createChannel, createTestServer } from './Common.js'

test('test send messages in a channel', async ({ page: user1, browser, secondUserState }) => {
  const user2Context = await browser.newContext({ storageState: secondUserState })
  const user2 = await user2Context.newPage()

  await user1.goto('/chat')

  const server = await createTestServer(user1)

  await expect(user1).toHaveURL(new RegExp(`.*/chat/server/${server.id}`))

  // Create Channel
  const channel = await createChannel(user1, server)

  await user2.goto(`/chat/server/${server.id}/channel/${channel.id}`)
  const wsPromise = user2.waitForEvent('websocket', (ws) => ws.url().includes('/ws'))

  const ws = await wsPromise
  const framePromise = ws.waitForEvent('framesent', {
    predicate: (event) => {
      try {
        const data = JSON.parse(event.payload.toString())
        return data.type == 1 && data.channel == channel.id
      } catch {
        return false
      }
    },
    timeout: 3000,
  })

  expect(framePromise).toBeTruthy()

  await expect(user2.locator('.chat-info')).toContainText(channel.name)

  await expect(
    user1.locator('.channel-name-row.background-light').locator('.channel-name')
  ).toHaveText(channel.name)

  await expect(user1.locator('.chat-info')).toContainText(channel.name)

  await expect(user1.locator('#messages-list > *')).toHaveCount(0)
  await expect(user2.locator('#messages-list > *')).toHaveCount(0)

  const message1 = 'this is a test'
  await user1.locator('#message-input').fill(message1)
  await user1.locator('#wsmessage-btn').click()

  await expect(user1.locator('#messages-list > *')).toHaveCount(1)
  await expect(user2.locator('#messages-list > *')).toHaveCount(1)

  await expect(user1.locator('#messages-list > *')).toContainText(message1)
  await expect(user2.locator('#messages-list > *')).toContainText(message1)

  await expect(user1.locator('#messages-list > .background-soft')).toHaveCount(1)
  await expect(user2.locator('#messages-list > .background-soft')).toHaveCount(0)
})

test('test send message with attachments', async ({ page: user1, browser, secondUserState }) => {
  const user2Context = await browser.newContext({ storageState: secondUserState })
  const user2 = await user2Context.newPage()

  await user1.goto('/chat')

  const server = await createTestServer(user1)

  await expect(user1).toHaveURL(new RegExp(`.*/chat/server/${server.id}`))

  // Create Channel
  const channel = await createChannel(user1, server)

  await user2.goto(`/chat/server/${server.id}/channel/${channel.id}`)
  const wsPromise = user2.waitForEvent('websocket', (ws) => ws.url().includes('/ws'))

  const ws = await wsPromise
  const framePromise = ws.waitForEvent('framesent', {
    predicate: (event) => {
      try {
        const data = JSON.parse(event.payload.toString())
        return data.type == 1 && data.channel == channel.id
      } catch {
        return false
      }
    },
    timeout: 3000,
  })

  expect(framePromise).toBeTruthy()

  await expect(user2.locator('.chat-info')).toContainText(channel.name)

  await expect(
    user1.locator('.channel-name-row.background-light').locator('.channel-name')
  ).toHaveText(channel.name)

  await expect(user1.locator('.chat-info')).toContainText(channel.name)

  await expect(user1.locator('#messages-list > *')).toHaveCount(0)
  await expect(user2.locator('#messages-list > *')).toHaveCount(0)

  const message1 = 'this is a test'
  await user1.locator('#message-input').fill(message1)

  const uploadImagePromise = user1.waitForResponse(
    (res) => res.url().includes('/api/v1/files') && res.request().method() === 'POST'
  )

  const fileName = 'png-test-32x32.png'

  const fileChooserPromise = user1.waitForEvent('filechooser')
  await user1.locator('#message-box-icons').getByText('add').click()
  const fileChooser = await fileChooserPromise
  await fileChooser.setFiles(`e2e/media/${fileName}`)

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
  expect(user1.locator('.file-preview-entry > img')).not.toBeNull()
  await expect(user1.locator('.file-preview-entry > img')).toHaveAttribute('src', image.url)

  await user1.locator('#wsmessage-btn').click()

  await expect(user1.locator('#messages-list > *')).toHaveCount(1)
  await expect(user2.locator('#messages-list > *')).toHaveCount(1)

  await expect(user1.locator('#messages-list > *')).toContainText(message1)
  await expect(user2.locator('#messages-list > *')).toContainText(message1)

  await expect(user1.locator('#messages-list > .background-soft')).toHaveCount(1)
  await expect(user2.locator('#messages-list > .background-soft')).toHaveCount(0)

  // User 1 chat
  await expect(user1.locator('.file-preview-entry > .file-preview-name')).toHaveText(fileName)
  expect(user1.locator('.file-preview-entry > img')).not.toBeNull()
  await expect(user1.locator('.file-preview-entry > img')).toHaveAttribute(
    'src',
    expect.any(String)
  )

  // User 2 chat
  await expect(user2.locator('.file-preview-entry > .file-preview-name')).toHaveText(fileName)
  expect(user2.locator('.file-preview-entry > img')).not.toBeNull()
  await expect(user2.locator('.file-preview-entry > img')).toHaveAttribute(
    'src',
    expect.any(String)
  )
})
