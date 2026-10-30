import { APIRequestContext } from '@playwright/test'
import { test, expect, acquireAccount } from '../fixtures.js'

// Reset storage state for this file to avoid being authenticated
test.use({ storageState: { cookies: [], origins: [] } })
const API_URL = '/api/v1/'

async function registerUser(
  request: APIRequestContext,
  user: { username: string; email: string; password: string }
) {
  const response = await request.post(`${API_URL}auth/register`, {
    data: { username: user.username, email: user.email, password: user.password },
  })
  expect(response.ok(), `${response.status()} ${await response.text()}`).toBeTruthy()
  return response
}

test('test login user bad request', async ({ page }) => {
  await page.goto('/login')

  const account = acquireAccount()

  const title = page.locator('h1')
  await expect(title).toHaveText('Welcome Back!')

  const logInBtn = page.getByRole('button', { name: 'Log In' })
  await expect(logInBtn).toBeDisabled()

  await page.getByLabel('Email').fill(account.email)
  await page.getByLabel('Password', { exact: true }).fill(account.password)

  await expect(logInBtn).toBeEnabled()
  await logInBtn.click()

  await expect(page.locator('.auth-error')).toHaveText('Something went wrong. Please try again.')
})

test('test login user ok', async ({ page, request }) => {
  await page.goto('/login')

  const account = acquireAccount()

  const title = page.locator('h1')
  await expect(title).toHaveText('Welcome Back!')

  await registerUser(request, account) //Register user

  await page.getByLabel('Email').fill(account.email)
  await page.getByLabel('Password', { exact: true }).fill(account.password)

  await page.getByRole('button', { name: 'Log In' }).click()

  await expect(page).toHaveURL(/\/chat/)

  const username = page.locator('#navbar-user')
  await expect(username).toHaveText(account.username)
})

test('test logged user redirects', async ({ page, request }) => {
  await page.goto('/login')

  const account = acquireAccount()

  const title = page.locator('h1')
  await expect(title).toHaveText('Welcome Back!')

  await registerUser(request, account) //Register user

  await page.getByLabel('Email').fill(account.email)
  await page.getByLabel('Password', { exact: true }).fill(account.password)

  await page.getByRole('button', { name: 'Log In' }).click()

  await expect(page).toHaveURL(/\/chat/)

  const username = page.locator('#navbar-user')
  expect(username).toHaveText(account.username)

  await page.goto('/login')
  await expect(page).toHaveURL(/\/chat/)

  await page.goto('/register')
  await expect(page).toHaveURL(/\/chat/)

  await page.goto('/')
  await expect(page).toHaveURL(/\//)
})

test('test login link', async ({ page }) => {
  await page.goto('/login')
  const link = page.locator('#login').getByRole('link', { name: 'Register' })
  link.click()

  await page.waitForURL(`/register`)
})
