import { A } from 'vue-router/dist/index-u4XkZVDG.js';
import { test, expect, acquireAccount } from '../fixtures.js';

// Reset storage state for this file to avoid being authenticated
test.use({ storageState: { cookies: [], origins: [] } });

test('test register user', async ({ page }) => {
    await page.goto('/register');

    const account = acquireAccount();

    const title = page.locator('h1');
    await expect(title).toHaveText("Create Account");

    const createAccountBtn = page.getByRole('button', { name: 'Create Account' });
    await expect(createAccountBtn).toBeDisabled();

    await page.getByLabel('Username').fill(account.username);
    await page.getByLabel('Email').fill(account.email);
    await page.getByLabel('Password', { exact: true }).fill(account.password);
    await page.getByLabel('Confirm Password', { exact: true }).fill('123');

    let passwordsDontMach = page.locator('.custom-form-field-error');
    await expect(passwordsDontMach).toBeVisible();
    await expect(passwordsDontMach).toHaveText("The passwords don't match.");


    await page.getByLabel('Confirm Password', { exact: true }).fill(account.password);
    await expect(passwordsDontMach).toBeHidden();

    await expect(createAccountBtn).toBeEnabled();
    await createAccountBtn.click();

    await expect(page).toHaveURL(/\/chat/);

    const username = page.locator('#navbar-user');
    expect(username).toHaveText(account.username);
})

test('test register user bad request', async ({ page }) => {
    await page.goto('/register');

    const title = page.locator('h1');
    await expect(title).toHaveText("Create Account");

    await page.getByLabel('Username').fill('Username1');
    await page.getByLabel('Email').fill("invalidEmail");
    await page.getByLabel('Password', { exact: true }).fill("password123");
    await page.getByLabel('Confirm Password', { exact: true }).fill('password123');
    await page.getByRole('button', { name: 'Create Account' }).click();

    await expect(page.locator('.auth-error')).toHaveText('Something went wrong. Please try again.');
})

test('test login link', async ({ page }) => {
    await page.goto('/register');
    const link = page.getByRole('link', { name: 'Log In' })
    link.click();

    await page.waitForURL(`/login`);
})