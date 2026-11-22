import { test, expect } from '../fixtures.js'
import { gotoAndWaitForData, sendMessage, tableRow, uniqueName } from '../chat/server/Common.js'
import {
  answerFriendRequest,
  expectFriendRequestError,
  friendLink,
  getDirectConversationIds,
  makeFriends,
  openDirectConversation,
  RECEIVED_FRIEND_REQUESTS_URL,
  revokeFriendRequest,
  sendFriendRequest,
  SENT_FRIEND_REQUESTS_URL,
  submitFriendRequest,
} from './Common.js'

// Each test registers several users and makes several round trips: more time than the default
test.describe.configure({ timeout: 60_000 })

// Every test uses new users: a friendship cannot be undone, so the users of the worker
// would not start the next test without friends

test.describe('friend requests', () => {
  test('a sent friend request is pending for both users', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await sendFriendRequest(user1.page, user2.account.username)

    // Sender
    await user1.page.goto(SENT_FRIEND_REQUESTS_URL)
    const sentRow = tableRow(user1.page, user2.account.username)
    await expect(sentRow).toHaveCount(1)
    await expect(sentRow.getByRole('button', { name: 'Revoke', exact: true })).toBeVisible()

    // Receiver, through the user settings menu
    await user2.page.goto('/settings/user/profile')
    await user2.page.getByRole('link', { name: 'Friend Requests', exact: true }).click()
    await user2.page.getByRole('link', { name: 'Received Friend Requests' }).click()
    await expect(user2.page).toHaveURL(new RegExp(`${RECEIVED_FRIEND_REQUESTS_URL}$`))

    const receivedRow = tableRow(user2.page, user1.account.username)
    await expect(receivedRow).toHaveCount(1)
    await expect(receivedRow.getByRole('button', { name: 'Accept', exact: true })).toBeVisible()
    await expect(receivedRow.getByRole('button', { name: 'Reject', exact: true })).toBeVisible()
  })

  test('accepting a friend request makes both users friends', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await sendFriendRequest(user1.page, user2.account.username)
    await answerFriendRequest(user2.page, user1.account.username, 'Accept')

    // Only pending requests are listed
    await gotoAndWaitForData(user1.page, SENT_FRIEND_REQUESTS_URL)
    await expect(tableRow(user1.page, user2.account.username)).toHaveCount(0)

    // Both users see the other one in the friends list, linked to the same conversation
    await user1.page.goto('/chat/direct')
    await user2.page.goto('/chat/direct')
    const link1 = friendLink(user1.page, user2.account.username)
    const link2 = friendLink(user2.page, user1.account.username)
    await expect(link1).toBeVisible()
    await expect(link2).toBeVisible()
    const href = await link1.getAttribute('href')
    expect(href).toMatch(/\/chat\/direct\/.+/)
    await expect(link2).toHaveAttribute('href', href!)

    // The link opens the conversation with the friend
    await link1.click()
    await expect(user1.page).toHaveURL(new RegExp(`${href}$`))
    await expect(link1).toHaveClass(/background-light/)
    await expect(user1.page.locator('.chat-info')).toContainText(user2.account.username)
    await expect(user1.page.locator('#message-input')).toHaveAttribute(
      'placeholder',
      `Write a message to ${user2.account.username}`
    )
  })

  test('rejecting a friend request removes it for both users', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await sendFriendRequest(user1.page, user2.account.username)
    await answerFriendRequest(user2.page, user1.account.username, 'Reject')

    await gotoAndWaitForData(user1.page, SENT_FRIEND_REQUESTS_URL)
    await expect(tableRow(user1.page, user2.account.username)).toHaveCount(0)

    // They are not friends and nothing is pending: the request can be sent again
    await sendFriendRequest(user1.page, user2.account.username)
    await user2.page.goto(RECEIVED_FRIEND_REQUESTS_URL)
    await expect(tableRow(user2.page, user1.account.username)).toHaveCount(1)
  })

  test('revoking a friend request removes it for both users', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await sendFriendRequest(user1.page, user2.account.username)
    await revokeFriendRequest(user1.page, user2.account.username)

    await gotoAndWaitForData(user2.page, RECEIVED_FRIEND_REQUESTS_URL)
    await expect(tableRow(user2.page, user1.account.username)).toHaveCount(0)

    // Nothing is pending: the request can be sent again
    await sendFriendRequest(user1.page, user2.account.username)
    await user2.page.goto(RECEIVED_FRIEND_REQUESTS_URL)
    await expect(tableRow(user2.page, user1.account.username)).toHaveCount(1)
  })

  test('a request to a user who had already sent one accepts it', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await sendFriendRequest(user1.page, user2.account.username)
    // The modal closes: the backend accepts the request of user1 instead of creating another
    await sendFriendRequest(user2.page, user1.account.username)

    await gotoAndWaitForData(user1.page, SENT_FRIEND_REQUESTS_URL)
    await expect(tableRow(user1.page, user2.account.username)).toHaveCount(0)

    await gotoAndWaitForData(user1.page, RECEIVED_FRIEND_REQUESTS_URL)
    await expect(tableRow(user1.page, user2.account.username)).toHaveCount(0)

    await user1.page.goto('/chat/direct')
    await expect(friendLink(user1.page, user2.account.username)).toBeVisible()
    await user2.page.goto('/chat/direct')
    await expect(friendLink(user2.page, user1.account.username)).toBeVisible()
  })

  test('cannot send a friend request to a user that does not exist', async ({ createUser }) => {
    const user1 = await createUser()

    await submitFriendRequest(user1.page, uniqueName('nobody'))
    await expectFriendRequestError(user1.page, 'Cannot find a user with this username')
  })

  test('cannot send a friend request to yourself', async ({ createUser }) => {
    const user1 = await createUser()

    await submitFriendRequest(user1.page, user1.account.username)
    await expectFriendRequestError(user1.page, 'You cannot send a friend request to yourself')
  })

  test('cannot send the same friend request twice', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await sendFriendRequest(user1.page, user2.account.username)
    await submitFriendRequest(user1.page, user2.account.username)
    await expectFriendRequestError(user1.page, 'You already sent a friend request to this user')

    // Still only one request
    await user2.page.goto(RECEIVED_FRIEND_REQUESTS_URL)
    await expect(tableRow(user2.page, user1.account.username)).toHaveCount(1)
  })

  test('cannot send a friend request to a friend', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()

    await makeFriends(user1, user2)

    await submitFriendRequest(user1.page, user2.account.username)
    await expectFriendRequestError(user1.page, 'You are already friends with this user')

    await submitFriendRequest(user2.page, user1.account.username)
    await expectFriendRequestError(user2.page, 'You are already friends with this user')
  })

  test('the send friend request modal can be closed', async ({ createUser }) => {
    const user1 = await createUser()

    await user1.page.goto('/chat/direct')
    await user1.page.getByRole('button', { name: 'Send friend request', exact: true }).click()
    await expect(user1.page.locator('.modal')).toBeVisible()

    await user1.page.locator('.modal-close').click()
    await expect(user1.page.locator('.modal')).not.toBeVisible()
  })
})

test.describe('direct messages', () => {
  test('without a conversation selected there is no chat', async ({ createUser }) => {
    const user1 = await createUser()

    await user1.page.goto('/chat/direct')
    await expect(user1.page.getByText('Start a new chat')).toBeVisible()
    await expect(user1.page.locator('#message-input')).toHaveCount(0)
  })

  test('friends can send messages to each other', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()
    await makeFriends(user1, user2)

    await openDirectConversation(user1.page, user2.account.username)
    await openDirectConversation(user2.page, user1.account.username)

    await expect(user1.page.locator('.chat-message')).toHaveCount(0)
    await expect(user2.page.locator('.chat-message')).toHaveCount(0)

    await sendMessage(user1.page, 'hello from user1')
    await expect(user2.page.locator('.chat-message')).toHaveCount(1)
    await expect(user2.page.locator('.chat-message')).toContainText('hello from user1')
    await expect(user2.page.locator('.chat-message')).toContainText(user1.account.username)

    await sendMessage(user2.page, 'hello from user2')
    await expect(user1.page.locator('.chat-message')).toHaveCount(2)

    // Newest first in the DOM (the list is shown with flex-col-reverse)
    await expect(user1.page.locator('.chat-message').first()).toContainText('hello from user2')

    // Each user only sees their own messages highlighted
    await expect(user1.page.locator('.chat-message.background-soft')).toHaveCount(1)
    await expect(user1.page.locator('.chat-message.background-soft')).toContainText(
      'hello from user1'
    )
    await expect(user2.page.locator('.chat-message.background-soft')).toContainText(
      'hello from user2'
    )

    // The messages are saved
    await user2.page.reload()
    await expect(user2.page.locator('.chat-message')).toHaveCount(2)
  })

  test('messages are only sent to their conversation', async ({ createUser }) => {
    // Three users and two friendships
    test.slow()
    const user1 = await createUser()
    const user2 = await createUser()
    const user3 = await createUser()
    await makeFriends(user1, user2)
    await makeFriends(user1, user3)

    // user3 writes first in its conversation with user1, so there is something to wait for
    const conversation13 = await openDirectConversation(user3.page, user1.account.username)
    await sendMessage(user3.page, 'hello from user3')

    const conversation12 = await openDirectConversation(user1.page, user2.account.username)
    expect(conversation12.chatConversationId).not.toBe(conversation13.chatConversationId)
    expect(conversation12.directConversationId).not.toBe(conversation13.directConversationId)
    await sendMessage(user1.page, 'only for user2')

    // user3 is subscribed to its own conversation with user1: nothing else arrives
    await user3.page.reload()
    await expect(user3.page.locator('.chat-message').filter({ hasText: 'hello from user3' })).toBeVisible()
    await expect(user3.page.locator('.chat-message')).toHaveCount(1)

    // user1 changes to the conversation with user3: only the message of user3 is there
    await friendLink(user1.page, user3.account.username).click()
    await expect(user1.page.locator('.chat-info')).toContainText(user3.account.username)
    await expect(user1.page.locator('.chat-message').filter({ hasText: 'hello from user3' })).toBeVisible()
    await expect(user1.page.locator('.chat-message')).toHaveCount(1)

    // And back to the conversation with user2
    await friendLink(user1.page, user2.account.username).click()
    await expect(user1.page.locator('.chat-message').filter({ hasText: 'only for user2' })).toBeVisible()
    await expect(user1.page.locator('.chat-message')).toHaveCount(1)
  })

  test('a user cannot open the conversation of two other users', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()
    const user3 = await createUser()
    await makeFriends(user1, user2)
    const { directConversationId } = await getDirectConversationIds(user1.page, user2.account.username)

    // The API does not return the conversation to user3, who is sent back to the friends list
    await user3.page.goto(`/chat/direct/${directConversationId}`)
    await expect(user3.page).toHaveURL(/\/chat\/direct$/)
    await expect(user3.page.getByText('Start a new chat')).toBeVisible()
  })
})
