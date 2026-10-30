import { APIRequestContext } from '@playwright/test'
import { test, expect, acquireAccount } from '../fixtures.js'

// Reset storage state for this file to avoid being authenticated
test.use({ storageState: { cookies: [], origins: [] } })

test('test redirect page not found', async ({ page }) => {
  await page.goto('/unknown')

  await expect(page.locator('h1')).toHaveText('404 Page not found.')
  await expect(page.locator('h2')).toHaveText('The page you requested cannot be found.')
  await expect(page.locator('h3')).toHaveText('Return to Home')
})
