import { test, expect } from '../../fixtures.js'
import { createChannel, createTestServer } from './Common.js'

test('test create and enter channel', async ({ page }) => {
  await page.goto('/chat')

  const server = await createTestServer(page)

  await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}`))

  // Create Channel
  const channel = await createChannel(page, server)

  await expect(
    page.locator('.channel-name-row.background-light').locator('.channel-name')
  ).toHaveText(channel.name)
})

test('test create and update channel', async ({ page }) => {
  await page.goto('/chat')

  const server = await createTestServer(page)

  await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}`))

  // Create Channel
  const channel = await createChannel(page, server)

  //Edit channel
  await page.locator('.channel-name-row.background-light').getByRole('button').click()

  await expect(page.locator('.modal')).toBeVisible()
  await expect(page.locator('.modal-title')).toContainText('Update Channel')
  await expect(
    page.locator('#update-channel-form').locator(`[aria-label="${server.name}"]`)
  ).toContainText(server.name)

  await expect(page.getByLabel('Channel Name')).toHaveValue(channel.name)
  const updatedChannel = {
    name: 'playwright-test-updated',
  }
  await page.getByLabel('Channel Name').fill(updatedChannel.name)

  const updateChannelBtn = page.getByRole('button', { name: 'Update Channel' })
  await expect(updateChannelBtn).toBeEnabled()

  const updateChannelResponse = page.waitForResponse(
    (res) => res.url().includes('/api/v1/chatchannels') && res.request().method() === 'PUT'
  )

  await updateChannelBtn.click()

  const updatedChannelResponse = await updateChannelResponse
  expect(updatedChannelResponse.status()).toBe(200)
  const bodyUpdatedChannel = await updatedChannelResponse.json()
  const updatedChannelId = bodyUpdatedChannel.id

  expect(channel.id).toBe(updatedChannelId)
  expect(bodyUpdatedChannel.name).toBe(updatedChannel.name)

  await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}/channel/${channel.id}`))
  await expect(page.locator('.modal')).not.toBeVisible()
})
