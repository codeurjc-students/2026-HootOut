import { type Locator, type Page, type Response, type WebSocket } from '@playwright/test'
import { randomUUID } from 'node:crypto'
import { expect } from '../../fixtures.js'

export type Server = { id: string; name: string; description: string }
export type Channel = { id: string; serverId: string; name: string; chatConversationId?: string }
export type ServerInvitation = { id: string; chatServerId: string; status: number; userId: string }

// Page size of the messages in the backend
export const MESSAGES_PAGE_SIZE = 20

// The users of a worker are shared by all its tests, so every entity gets a unique name.
// This way a test that failed half way does not leave rows or buttons that match the next one
export function uniqueName(prefix: string): string {
  return `${prefix}-${randomUUID().slice(0, 8)}`
}

// Id used to subscribe to the channel through the websocket
export function conversationIdOf(channel: Channel): string {
  return channel.chatConversationId ?? channel.id
}

// Row of a PaginatedTable. tbody tr instead of getByRole('row'): on mobile the table is shown
// with display: block, and some browsers then drop the table roles
export function tableRow(page: Page, text: string): Locator {
  return page.locator('tbody tr').filter({ hasText: text })
}

/**
 * Resolves when the page subscribes to the conversation through the websocket.
 * Call it BEFORE navigating: the socket opens while the page loads, and the frames sent
 * before a listener is attached are not reported.
 */
export function waitForSubscription(
  page: Page,
  conversationId: string,
  timeout = 10_000
): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      page.off('websocket', onSocket)
      reject(new Error(`The page did not subscribe to conversation ${conversationId}`))
    }, timeout)

    function onFrame(event: { payload: string | Buffer }) {
      try {
        const data = JSON.parse(event.payload.toString())
        if (data.type == 1 && data.channel == conversationId) {
          clearTimeout(timer)
          page.off('websocket', onSocket)
          resolve()
        }
      } catch {
        // Not a JSON frame
      }
    }

    function onSocket(ws: WebSocket) {
      if (ws.url().includes('/ws')) ws.on('framesent', onFrame)
    }

    page.on('websocket', onSocket)
  })
}

// Navigates to the url and waits until the page is subscribed to the conversation
export async function gotoConversation(
  page: Page,
  url: string,
  conversationId: string
): Promise<void> {
  const subscribed = waitForSubscription(page, conversationId)
  await page.goto(url)
  await subscribed
}

export async function createTestServer(page: Page, name = uniqueName('server')): Promise<Server> {
  await page.getByRole('button', { name: 'Create server', exact: true }).click()

  await expect(page.locator('.modal')).toBeVisible()
  await expect(page.locator('.modal-title')).toContainText('Create New Server')

  const submitBtn = page.getByRole('button', { name: 'Create Server', exact: true })
  await expect(submitBtn).toBeDisabled()

  const responsePromise = page.waitForResponse(
    (res) => res.url().includes('/api/v1/chatServers') && res.request().method() === 'POST'
  )

  const server = {
    name,
    description: 'test',
  }

  await page.getByLabel('Server Name').fill(server.name)
  await page.getByLabel('Server Description', { exact: true }).fill(server.description)

  await expect(submitBtn).toBeEnabled()
  await submitBtn.click()

  const response = await responsePromise
  expect(response.status()).toBe(200)
  const body = await response.json()
  const id = body.id

  expect(id).toBeTruthy()
  expect(body.name).toBe(server.name)
  expect(body.description).toBe(server.description)
  await expect(page.locator('.modal')).not.toBeVisible()

  await expect(page).toHaveURL(new RegExp(`.*/chat/server/${id}`))
  return { id: id, name: server.name, description: server.description }
}

export async function createChannel(
  page: Page,
  server: Server,
  name = uniqueName('channel')
): Promise<Channel> {
  await page.getByRole('button', { name: 'Create channel', exact: true }).click()

  await expect(page.locator('.modal')).toBeVisible()
  await expect(page.locator('.modal-title')).toContainText('Create New Channel')
  await expect(
    page.locator('#create-channel-form').locator(`[aria-label="${server.name}"]`)
  ).toContainText(server.name)

  const submitChannelBtn = page.getByRole('button', { name: 'Create Channel', exact: true })
  await expect(submitChannelBtn).toBeDisabled()

  const createChannelPromise = page.waitForResponse(
    (res) => res.url().includes('/api/v1/chatchannels') && res.request().method() === 'POST'
  )

  await page.getByLabel('Channel Name').fill(name)

  await expect(submitChannelBtn).toBeEnabled()
  await submitChannelBtn.click()

  const responseChannel = await createChannelPromise
  expect(responseChannel.status()).toBe(200)
  const bodyChannel = await responseChannel.json()
  const channelId = bodyChannel.id

  expect(channelId).toBeTruthy()
  expect(bodyChannel.name).toBe(name)

  await expect(page).toHaveURL(new RegExp(`.*/chat/server/${server.id}/channel/${channelId}`))

  await expect(page.locator('.modal')).not.toBeVisible()

  return {
    id: channelId,
    serverId: server.id,
    name,
    chatConversationId: bodyChannel.chatConversationId,
  }
}

export function channelUrl(channel: Channel): string {
  return `/chat/server/${channel.serverId}/channel/${channel.id}`
}

// Opens the invite modal, sends the invitation and returns the response, whatever its status
export async function submitInvitation(
  page: Page,
  server: Server,
  username: string
): Promise<Response> {
  await page.locator('.inviteMemberBtn').click()

  await expect(page.locator('.modal')).toBeVisible()
  await expect(page.locator('.modal-title')).toContainText('Invite Member')
  await expect(
    page.locator('#invite-member-form').locator(`[aria-label="${server.name}"]`)
  ).toContainText(server.name)

  const submitInvitationBtn = page.locator('.modal').getByRole('button', { name: 'Invite Member', exact: true })
  await expect(submitInvitationBtn).toBeDisabled()

  const sendInvitationPromise = page.waitForResponse(
    (res) =>
      res.url().includes('/api/v1/chatservers/invitation') && res.request().method() === 'POST'
  )

  await page.locator('#invite-member-form').getByLabel('Username').fill(username)

  await expect(submitInvitationBtn).toBeEnabled()
  await submitInvitationBtn.click()

  return await sendInvitationPromise
}

export async function inviteUserToServer(
  page: Page,
  server: Server,
  username: string
): Promise<ServerInvitation> {
  const responseInvitation = await submitInvitation(page, server, username)
  expect(responseInvitation.status()).toBe(200)
  const invitation = await responseInvitation.json()

  expect(invitation).toBeTruthy()
  expect(invitation.id).toBeTruthy()
  expect(invitation.chatServerId).toBe(server.id)

  return invitation
}

export async function openUserInvitations(page: Page): Promise<void> {
  await page.goto('/chat')
  await page.locator('#navbar-user').locator(`[aria-controls="navbar-user-dropdown"]`).click()

  await page.locator('#navbar-user').locator('[href="/settings/user/profile"]').click()
  await expect(page).toHaveURL(new RegExp(`.*settings/user/profile`))

  await page.locator('[href="/settings/user/invitations"]').click()
  await expect(page).toHaveURL(new RegExp(`.*settings/user/invitations`))
}

// Accepts the invitation to the server. Accepting reloads the page
export async function acceptInvitation(page: Page, server: Server): Promise<void> {
  await openUserInvitations(page)

  const row = tableRow(page, server.name)
  await expect(row).toHaveCount(1)

  const joinRequest = page.waitForResponse(
    (res) =>
      res.url().includes('/api/v1/chatservers/invitation') && res.request().method() === 'PUT'
  )
  const reload = page.waitForEvent('load')

  await row.getByRole('button', { name: 'Join', exact: true }).click()

  const joinResponse = await joinRequest
  expect(joinResponse.status()).toBe(200)
  await reload
  // Let the reloaded page finish its requests (session included) before navigating again:
  // leaving it half way can lose the session (see the README)
  await page.waitForLoadState('networkidle')
}

/**
 * user1 creates a server with a channel and invites user2, who accepts the invitation.
 * Both pages end up in the channel, subscribed to it.
 */
export async function setupChannelWithTwoUsers(
  user1: Page,
  user2: Page,
  user2Username: string
): Promise<{ server: Server; channel: Channel }> {
  await user1.goto('/chat')
  const server = await createTestServer(user1)
  const channel = await createChannel(user1, server)

  await inviteUserToServer(user1, server, user2Username)
  await acceptInvitation(user2, server)

  const conversationId = conversationIdOf(channel)
  await gotoConversation(user1, channelUrl(channel), conversationId)
  await gotoConversation(user2, channelUrl(channel), conversationId)

  await expect(user1.locator('.chat-info')).toContainText(channel.name)
  await expect(user2.locator('.chat-info')).toContainText(channel.name)

  return { server, channel }
}

// Time for the ack of a message. Longer than the default of expect: with several workers
// and Firefox, the round trip through the websocket can take more than 3 seconds
export const ACK_TIMEOUT = 10_000

// Sends a text message and waits until it is confirmed (no pending messages left)
export async function sendMessage(page: Page, text: string): Promise<void> {
  await page.locator('#message-input').fill(text)
  await page.getByRole('button', { name: 'Send message', exact: true }).click()
  await expect(page.locator('.chat-message').filter({ hasText: text }).first()).toBeVisible({
    timeout: ACK_TIMEOUT,
  })
  await expect(page.locator('.pending-message')).toHaveCount(0)
}

/**
 * Selects a file with the "Attach file" button (files are in e2e/media).
 * By default it waits until the upload finishes. Pass waitForUpload = false when the test
 * holds the upload with page.route
 */
export async function attachFile(page: Page, fileName: string, waitForUpload = true): Promise<void> {
  const uploadResponse = waitForUpload
    ? page.waitForResponse(
        (res) => res.url().includes('/api/v1/files') && res.request().method() === 'POST'
      )
    : null
  const fileChooserPromise = page.waitForEvent('filechooser')
  await page.getByRole('button', { name: 'Attach file', exact: true }).click()
  const fileChooser = await fileChooserPromise
  await fileChooser.setFiles(`e2e/media/${fileName}`)

  if (uploadResponse) {
    expect((await uploadResponse).status()).toBe(200)
  }
}

/**
 * Navigates and waits until the page has no requests in flight, so the tables and lists
 * have loaded. Needed before checking that something is NOT shown: PaginatedTable has
 * no empty or loading state to wait for. The websocket does not count as a request
 */
export async function gotoAndWaitForData(page: Page, url: string): Promise<void> {
  await page.goto(url)
  await page.waitForLoadState('networkidle')
}
