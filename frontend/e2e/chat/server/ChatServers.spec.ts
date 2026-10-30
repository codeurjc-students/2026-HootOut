import { test, expect } from '../../fixtures.js'
import { createTestServer } from './Common.js'

test('test close server modal', async ({ page }) => {
  await page.goto('/chat')
  await page.locator('#new-server-btn').click()

  const modal = page.locator('.modal')
  await expect(modal).toBeVisible()
  await expect(page.locator('.modal-title')).toContainText('Create New Server')

  const closeBtn = page.locator('.modal-close')
  await closeBtn.click()

  await expect(modal).not.toBeVisible()
})

test('test create and enter server', async ({ page }) => {
  await page.goto('/chat')

  const server = await createTestServer(page)

  await page.goto('/chat')
  const serverList = page.locator('#server-list')
  const serverbtn = serverList.locator(`[server-id="${server.id}"]`)

  expect(serverbtn).not.toBeNull()

  await serverbtn.click()

  await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}`))
})
