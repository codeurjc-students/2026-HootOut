import { type Locator, type Page } from '@playwright/test'
import { test, expect } from '../fixtures.js'
import {
  acceptInvitation,
  createTestServer,
  inviteUserToServer,
  tableRow,
  type Server,
} from '../chat/server/Common.js'
import { friendLink, makeFriends } from '../friends/Common.js'

// Each test registers several users and makes several round trips: more time than the default
test.describe.configure({ timeout: 60_000 })

// The backend waits 5 s before announcing that a user went offline (grace period), and the
// event still has to go through RabbitMQ and the websocket
const OFFLINE_TIMEOUT = 15_000
const ONLINE_TIMEOUT = 10_000

function presenceOf(container: Locator): Locator {
  return container.locator('[data-presence]')
}

// Opens the direct messages view, which connects the websocket: the user is online
async function goOnline(page: Page): Promise<void> {
  await page.goto('/chat/direct')
  await expect(page.locator('.chat-left-bar')).toBeVisible()
}

// Leaves the app: the websocket is closed and the user goes offline after the grace period
async function goOffline(page: Page): Promise<void> {
  await page.goto('about:blank')
}

test.describe('presence of friends', () => {
  // A friendship is created while the websocket is connected: the subscriptions to the
  // presence of the friends are made when connecting, so goOnline() reloads the page

  test('a friend is shown online while it has the app open', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()
    await makeFriends(user1, user2)
    await goOffline(user2.page)

    await goOnline(user1.page)
    const friend = friendLink(user1.page, user2.account.username)
    await expect(presenceOf(friend)).toHaveAttribute('data-presence', 'offline')

    await goOnline(user2.page)

    await expect(presenceOf(friend)).toHaveAttribute('data-presence', 'online', {
      timeout: ONLINE_TIMEOUT,
    })
  })

  test('a friend is shown offline after closing the app', async ({ createUser }) => {
    const user1 = await createUser()
    const user2 = await createUser()
    await makeFriends(user1, user2)

    await goOnline(user2.page)
    await goOnline(user1.page)
    const friend = friendLink(user1.page, user2.account.username)
    // Online from the snapshot of the friends that arrives when connecting
    await expect(presenceOf(friend)).toHaveAttribute('data-presence', 'online', {
      timeout: ONLINE_TIMEOUT,
    })

    await goOffline(user2.page)

    await expect(presenceOf(friend)).toHaveAttribute('data-presence', 'offline', {
      timeout: OFFLINE_TIMEOUT,
    })
  })

  test('reloading the page does not show the user offline to its friends', async ({
    createUser,
  }) => {
    const user1 = await createUser()
    const user2 = await createUser()
    await makeFriends(user1, user2)
    await goOnline(user2.page)
    await goOnline(user1.page)
    const friend = friendLink(user1.page, user2.account.username)
    await expect(presenceOf(friend)).toHaveAttribute('data-presence', 'online', {
      timeout: ONLINE_TIMEOUT,
    })

    // It reconnects inside the grace period
    await user2.page.reload()
    await expect(user2.page.locator('.chat-left-bar')).toBeVisible()
    // Longer than the grace period
    await user1.page.waitForTimeout(7_000)

    await expect(presenceOf(friend)).toHaveAttribute('data-presence', 'online')
  })
})

test.describe('presence of server members', () => {
  async function setupServer(
    owner: Page,
    memberPage: Page,
    memberUsername: string,
  ): Promise<Server> {
    await owner.goto('/chat')
    const server = await createTestServer(owner)
    await inviteUserToServer(owner, server, memberUsername)
    await acceptInvitation(memberPage, server)
    return server
  }

  const activeMembersUrl = (server: Server) => `/settings/server/${server.id}/members/active`

  test('the active members show who is online', async ({ createUser }) => {
    const owner = await createUser()
    const member = await createUser()
    const server = await setupServer(owner.page, member.page, member.account.username)
    await goOffline(member.page)

    // Loaded from its url: the page connects the websocket and subscribes to the server
    await owner.page.goto(activeMembersUrl(server))
    const memberRow = tableRow(owner.page, member.account.username)
    const ownerRow = tableRow(owner.page, owner.account.username)
    await expect(memberRow).toHaveCount(1)
    await expect(presenceOf(memberRow)).toHaveAttribute('data-presence', 'offline')
    // The owner is online: it has the page open
    await expect(presenceOf(ownerRow)).toHaveAttribute('data-presence', 'online', {
      timeout: ONLINE_TIMEOUT,
    })

    // They are not friends: the presence arrives through the subscription to the server
    await member.page.goto('/chat')

    await expect(presenceOf(memberRow)).toHaveAttribute('data-presence', 'online', {
      timeout: ONLINE_TIMEOUT,
    })

    await goOffline(member.page)

    await expect(presenceOf(memberRow)).toHaveAttribute('data-presence', 'offline', {
      timeout: OFFLINE_TIMEOUT,
    })
  })

  test('the members of a server that the user is in show their presence when it is opened from the chat', async ({
    createUser,
  }) => {
    const owner = await createUser()
    const member = await createUser()
    const server = await setupServer(owner.page, member.page, member.account.username)
    await member.page.goto('/chat')

    // From the chat to the settings without reloading: the websocket stays open
    await owner.page.goto(`/chat/server/${server.id}`)
    await expect(owner.page.locator('.chat-left-bar')).toBeVisible()
    await owner.page.evaluate((url) => {
      history.pushState({}, '', url)
      dispatchEvent(new PopStateEvent('popstate'))
    }, activeMembersUrl(server))

    const memberRow = tableRow(owner.page, member.account.username)
    await expect(presenceOf(memberRow)).toHaveAttribute('data-presence', 'online', {
      timeout: ONLINE_TIMEOUT,
    })
  })
})
