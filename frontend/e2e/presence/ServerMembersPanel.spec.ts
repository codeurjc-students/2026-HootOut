import { type Locator, type Page } from '@playwright/test'
import { test, expect } from '../fixtures.js'
import {
  acceptInvitation,
  createTestServer,
  inviteUserToServer,
  type Server,
} from '../chat/server/Common.js'

// Each test registers several users and makes several round trips: more time than the default
test.describe.configure({ timeout: 60_000 })

// Same margins as Presence.spec.ts: 5 s of grace period in the backend before an offline
const OFFLINE_TIMEOUT = 15_000
const ONLINE_TIMEOUT = 10_000

// The panel is only shown on wide screens (>= 1280 px)
test.use({ viewport: { width: 1440, height: 900 } })

const onlineList = (page: Page): Locator => page.locator('#online-members')
const offlineList = (page: Page): Locator => page.locator('#offline-members')
// The rows of both lists have the class "member" (the offline list is an InfiniteScrollList,
// which decides its own markup)
const memberIn = (list: Locator, username: string): Locator =>
  list.locator('.member').filter({ hasText: username })

async function setupServer(owner: Page, memberPage: Page, memberUsername: string): Promise<Server> {
  await owner.goto('/chat')
  const server = await createTestServer(owner)
  await inviteUserToServer(owner, server, memberUsername)
  await acceptInvitation(memberPage, server)
  return server
}

async function openServer(page: Page, server: Server): Promise<void> {
  await page.goto(`/chat/server/${server.id}`)
  await expect(page.locator('#server-members')).toBeVisible()
}

test.describe('online and offline members of a server', () => {
  test('a member moves between the lists when it opens and closes the app', async ({
    createUser,
  }) => {
    const owner = await createUser()
    const member = await createUser()
    const server = await setupServer(owner.page, member.page, member.account.username)
    await member.page.goto('about:blank')

    await openServer(owner.page, server)
    // The owner has the page open
    await expect(memberIn(onlineList(owner.page), owner.account.username)).toHaveCount(1, {
      timeout: ONLINE_TIMEOUT,
    })
    await expect(memberIn(offlineList(owner.page), member.account.username)).toHaveCount(1, {
      timeout: OFFLINE_TIMEOUT,
    })

    // They are not friends: the presence arrives through the subscription to the server
    await member.page.goto('/chat')

    await expect(memberIn(onlineList(owner.page), member.account.username)).toHaveCount(1, {
      timeout: ONLINE_TIMEOUT,
    })
    await expect(memberIn(offlineList(owner.page), member.account.username)).toHaveCount(0)
    await expect(owner.page.locator('#online-members-title')).toHaveText('Online — 2')
    await expect(owner.page.locator('#offline-members-title')).toHaveText('Offline — 0')

    await member.page.goto('about:blank')

    await expect(memberIn(offlineList(owner.page), member.account.username)).toHaveCount(1, {
      timeout: OFFLINE_TIMEOUT,
    })
    await expect(memberIn(onlineList(owner.page), member.account.username)).toHaveCount(0)
    await expect(owner.page.locator('#online-members-title')).toHaveText('Online — 1')
  })

  test('a member that is already online is in the online list when the server is opened', async ({
    createUser,
  }) => {
    const owner = await createUser()
    const member = await createUser()
    const server = await setupServer(owner.page, member.page, member.account.username)
    await member.page.goto('/chat')

    await openServer(owner.page, server)

    await expect(memberIn(onlineList(owner.page), member.account.username)).toHaveCount(1, {
      timeout: ONLINE_TIMEOUT,
    })
  })

  test('the panel is hidden on narrow screens', async ({ createUser }) => {
    const owner = await createUser()
    await owner.page.setViewportSize({ width: 1024, height: 800 })
    await owner.page.goto('/chat')
    const server = await createTestServer(owner.page)

    await owner.page.goto(`/chat/server/${server.id}`)
    await expect(owner.page.locator('.chat-left-bar')).toBeVisible()

    await expect(owner.page.locator('#server-members')).toBeHidden()
  })
})
