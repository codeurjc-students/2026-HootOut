import { type Locator, type Page } from '@playwright/test'
import { expect, type NewUser } from '../fixtures.js'
import { gotoConversation, tableRow } from '../chat/server/Common.js'

export const RECEIVED_FRIEND_REQUESTS_URL = '/settings/user/friendRequests/received'
export const SENT_FRIEND_REQUESTS_URL = '/settings/user/friendRequests/sent'

// Opens the send friend request modal and submits the username, without checking the result
export async function submitFriendRequest(page: Page, username: string): Promise<void> {
  await page.goto('/chat/direct')
  const modal = page.locator('.modal')
  // Retried: the click can arrive before the view is ready to open the modal
  await expect(async () => {
    // With a timeout: actionTimeout is 0, and a click that cannot be done would never end
    await page
      .getByRole('button', { name: 'Send friend request', exact: true })
      .click({ timeout: 2000 })
    await expect(modal).toBeVisible({ timeout: 1000 })
  }).toPass()
  await expect(page.locator('.modal-title')).toContainText('Send Friend Request')

  const submitBtn = modal.getByRole('button', { name: 'Send Friend Request', exact: true })
  await expect(submitBtn).toBeDisabled()

  await page.locator('#friend-request-username').fill(username)
  await expect(submitBtn).toBeEnabled()
  await submitBtn.click()
}

// Sends a friend request that must succeed: the modal closes
export async function sendFriendRequest(page: Page, username: string): Promise<void> {
  await submitFriendRequest(page, username)
  await expect(page.locator('.modal')).not.toBeVisible()
}

// The request was rejected by the backend: the modal stays open with the error of the field
export async function expectFriendRequestError(page: Page, message: string): Promise<void> {
  const form = page.locator('#send-friend-request-form')
  await expect(form.getByRole('alert')).toHaveText('Please review the form.')
  await expect(form.locator('.custom-form-field-error')).toHaveText(message)
  await expect(page.locator('.modal')).toBeVisible()
}

// Accepts or rejects the pending request received from the user. The row disappears
export async function answerFriendRequest(
  page: Page,
  fromUsername: string,
  action: 'Accept' | 'Reject'
): Promise<void> {
  await page.goto(RECEIVED_FRIEND_REQUESTS_URL)
  const row = tableRow(page, fromUsername)
  await expect(row).toHaveCount(1)

  await row.getByRole('button', { name: action, exact: true }).click()
  await expect(row).toHaveCount(0)
}

// Revokes the pending request sent to the user. The row disappears
export async function revokeFriendRequest(page: Page, toUsername: string): Promise<void> {
  await page.goto(SENT_FRIEND_REQUESTS_URL)
  const row = tableRow(page, toUsername)
  await expect(row).toHaveCount(1)

  await row.getByRole('button', { name: 'Revoke', exact: true }).click()
  await expect(row).toHaveCount(0)
}

// user1 sends a friend request to user2, who accepts it
export async function makeFriends(user1: NewUser, user2: NewUser): Promise<void> {
  await sendFriendRequest(user1.page, user2.account.username)
  await answerFriendRequest(user2.page, user1.account.username, 'Accept')
}

// Link of a friend in the friends list of the direct messages view
export function friendLink(page: Page, username: string): Locator {
  return page.locator('.chat-left-bar').getByRole('link', { name: username })
}

export type DirectConversationIds = { directConversationId: string; chatConversationId: string }

/**
 * Loads the direct messages view and returns the ids of the conversation with the friend,
 * taken from the friends list response (GET /api/v1/friends).
 * The url uses directConversationId and the websocket subscription uses chatConversationId
 */
export async function getDirectConversationIds(
  page: Page,
  friendUsername: string
): Promise<DirectConversationIds> {
  const friendsResponse = page.waitForResponse(
    (res) =>
      new URL(res.url()).pathname.endsWith('/api/v1/friends') && res.request().method() === 'GET'
  )
  await page.goto('/chat/direct')
  const body = await (await friendsResponse).json()
  const friend = body.items.find(
    (x: { friend: { username: string } }) => x.friend.username === friendUsername
  )
  expect(friend, `${friendUsername} is not in the first page of friends`).toBeTruthy()
  expect(friend.directConversationId).toBeTruthy()
  expect(friend.chatConversationId).toBeTruthy()

  await expect(friendLink(page, friendUsername)).toHaveAttribute(
    'href',
    `/chat/direct/${friend.directConversationId}`
  )
  return {
    directConversationId: friend.directConversationId,
    chatConversationId: friend.chatConversationId,
  }
}

/**
 * Opens the direct conversation with the friend and waits until the page is subscribed to it.
 * The page is loaded again with the url of the conversation: the websocket is already open in
 * the direct messages view, and only the frames of a new socket can be awaited.
 * When loaded from the url, the view asks the API for the conversation of directConversationId
 */
export async function openDirectConversation(
  page: Page,
  friendUsername: string
): Promise<DirectConversationIds> {
  const ids = await getDirectConversationIds(page, friendUsername)

  await gotoConversation(page, `/chat/direct/${ids.directConversationId}`, ids.chatConversationId)
  await expect(page.locator('.chat-info')).toContainText(friendUsername)
  return ids
}
