import { test, expect } from '../../fixtures.js'
import {
  acceptInvitation,
  channelUrl,
  createChannel,
  createTestServer,
  gotoAndWaitForData,
  inviteUserToServer,
  openUserInvitations,
  submitInvitation,
  tableRow,
  uniqueName,
  type Server,
} from './Common.js'
import { type Page } from '@playwright/test'

// The invited user is not a member: the server is not listed and its url redirects to /chat
async function expectNotMember(page: Page, server: Server) {
  await gotoAndWaitForData(page, '/chat')
  await expect(page.locator('#server-list').getByRole('link', { name: server.name })).toHaveCount(0)

  await page.goto(`/chat/server/${server.id}`)
  await expect(page).toHaveURL(/\/chat$/)
}

// The backend answers 400 and the modal stays open with the error
async function expectInvitationError(page: Page, server: Server, username: string) {
  const response = await submitInvitation(page, server, username)
  expect(response.status()).toBe(400)
  await expect(page.locator('#invite-member-form').getByRole('alert')).toBeVisible()
  await expect(page.locator('.modal')).toBeVisible()
}

test.describe('server invitations', () => {
  test('the invited user sees the pending invitation', async ({
    page: user1,
    secondPage: user2,
    workerAccount,
    secondAccount,
  }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)
    await inviteUserToServer(user1, server, secondAccount.username)

    await openUserInvitations(user2)
    const row = tableRow(user2, server.name)
    await expect(row).toHaveCount(1)
    await expect(row.locator('td[data-label="Invited By"]')).toHaveText(workerAccount.username)
    await expect(row.getByRole('button', { name: 'Join', exact: true })).toBeEnabled()
    await expect(row.getByRole('button', { name: 'Reject', exact: true })).toBeEnabled()

    // Being invited does not make the user a member
    await expectNotMember(user2, server)
  })

  test('joining a server makes the user a member', async ({ page: user1, createUser }) => {
    // A new user: the server list of the worker user grows with every test, and the new
    // server may not be in the first page of the list
    const { page: user2, account: account2 } = await createUser()

    await user1.goto('/chat')
    const server = await createTestServer(user1)
    const channel = await createChannel(user1, server)
    await inviteUserToServer(user1, server, account2.username)

    await acceptInvitation(user2, server)

    // Only pending invitations are listed
    await gotoAndWaitForData(user2, '/settings/user/invitations')
    await expect(tableRow(user2, server.name)).toHaveCount(0)

    // The server is in the list and its channels can be opened
    await user2.goto('/chat')
    const serverLink = user2.locator('#server-list').getByRole('link', { name: server.name })
    await expect(serverLink).toBeVisible()
    await serverLink.click()
    await expect(user2).toHaveURL(new RegExp(`/chat/server/${server.id}$`))

    await user2.locator('.channel-name', { hasText: channel.name }).click()
    await expect(user2).toHaveURL(new RegExp(`${channelUrl(channel)}$`))
    await expect(user2.locator('.chat-info')).toContainText(channel.name)
  })

  test('rejecting an invitation removes it', async ({
    page: user1,
    secondPage: user2,
    secondAccount,
  }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)
    await inviteUserToServer(user1, server, secondAccount.username)

    await openUserInvitations(user2)
    const row = tableRow(user2, server.name)
    await row.getByRole('button', { name: 'Reject', exact: true }).click()
    await expect(row).toHaveCount(0)

    await expectNotMember(user2, server)

    // Nothing is pending: the user can be invited again
    await user1.goto(`/chat/server/${server.id}`)
    await inviteUserToServer(user1, server, secondAccount.username)
    await openUserInvitations(user2)
    await expect(tableRow(user2, server.name)).toHaveCount(1)
  })

  test('revoking an invitation from the server settings cancels it', async ({
    page: user1,
    secondPage: user2,
    secondAccount,
  }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)
    await inviteUserToServer(user1, server, secondAccount.username)

    await user1.goto(`/settings/server/${server.id}/members/invitations`)
    // The server table lists every invitation with its status, and only the pending ones
    // can be revoked: after revoking, the row stays without the button
    const row = tableRow(user1, secondAccount.username)
    await expect(row).toHaveCount(1)
    const status = row.locator('td[data-label="Status"]')
    const pendingStatus = (await status.textContent())?.trim() ?? ''
    expect(pendingStatus).not.toBe('')

    await row.getByRole('button', { name: 'Revoke', exact: true }).click()
    await expect(row.getByRole('button', { name: 'Revoke', exact: true })).toHaveCount(0)
    await expect(status).not.toHaveText(pendingStatus)

    await gotoAndWaitForData(user2, '/settings/user/invitations')
    await expect(tableRow(user2, server.name)).toHaveCount(0)
    await expectNotMember(user2, server)
  })

  test('cannot invite a user that does not exist', async ({ page: user1 }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)

    await expectInvitationError(user1, server, uniqueName('nobody'))
  })

  test('cannot invite yourself', async ({ page: user1, workerAccount }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)

    await expectInvitationError(user1, server, workerAccount.username)
  })

  test('cannot invite the same user twice', async ({
    page: user1,
    secondPage: user2,
    secondAccount,
  }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)
    await inviteUserToServer(user1, server, secondAccount.username)

    await user1.goto(`/chat/server/${server.id}`)
    await expectInvitationError(user1, server, secondAccount.username)

    // Still only one invitation
    await openUserInvitations(user2)
    await expect(tableRow(user2, server.name)).toHaveCount(1)
  })

  test('cannot invite a member of the server', async ({
    page: user1,
    secondPage: user2,
    secondAccount,
  }) => {
    await user1.goto('/chat')
    const server = await createTestServer(user1)
    await inviteUserToServer(user1, server, secondAccount.username)
    await acceptInvitation(user2, server)

    await user1.goto(`/chat/server/${server.id}`)
    await expectInvitationError(user1, server, secondAccount.username)
  })
})
