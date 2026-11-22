import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import UserServerInvitations from '@/components/user/settings/UserServerInvitations.vue'
import { dateTimeMapper } from '@/services/common/utils'
import { InvitationStatus, type PageResult, type ServerInvitation } from '@/types'

// ---------- mocks ----------

const serverService = vi.hoisted(() => ({
  getUserServerInvitations: vi.fn(),
  updateServerInvitation: vi.fn(),
}))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: serverService }))

// ---------- helpers ----------

// The table shows serverName: the API sends it with the invitation
type UserServerInvitation = ServerInvitation & { serverName: string }

function invitation(id: number): UserServerInvitation {
  return {
    id: `invitation-${id}`,
    chatServerId: `server-${id}`,
    chatServerName: `Server ${id}`,
    serverName: `Server ${id}`,
    userId: `user-${id}`,
    username: `user-${id}`,
    status: InvitationStatus.Pending,
    createdAt: '2026-11-05T10:42:00Z',
    modifiedAt: '2026-11-06T09:15:00Z',
  }
}

function page(
  items: ServerInvitation[],
  next: string | null = null,
  total = items.length
): PageResult<ServerInvitation> {
  return { items, next, total, pageSize: 10 }
}

// A promise the test resolves or rejects when it wants
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

let router: Router

function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings/user', name: 'userSettings', component: placeholder },
      { path: '/settings/user/invitations', name: 'userServerInvitations', component: placeholder },
    ],
  })
}

async function mountInvitations() {
  await router.push('/settings/user/invitations')
  await router.isReady()
  const wrapper = mount(UserServerInvitations, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

const rows = (wrapper: VueWrapper) => wrapper.findAll('tbody tr')
const cells = (wrapper: VueWrapper, row: number) => rows(wrapper)[row]!.findAll('td')
const button = (wrapper: VueWrapper, row: number, text: 'Join' | 'Reject') =>
  rows(wrapper)
    [row]!.findAll('button')
    .find((b) => b.text() === text)!

async function click(wrapper: VueWrapper, row: number, text: 'Join' | 'Reject') {
  await button(wrapper, row, text).trigger('click')
  await flushPromises()
}

// ---------- tests ----------

describe('UserServerInvitations.vue', () => {
  let go: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    vi.resetAllMocks()
    router = createTestRouter()
    // router.go(0) reloads the page: replaced so jsdom does not try to navigate
    go = vi.spyOn(router, 'go').mockImplementation(() => {})
    serverService.getUserServerInvitations.mockResolvedValue(page([invitation(1), invitation(2)]))
    serverService.updateServerInvitation.mockResolvedValue(undefined)
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('header', () => {
    it('links back to the user settings', async () => {
      const wrapper = await mountInvitations()

      const back = wrapper.findAll('a').find((a) => a.text().includes('Server Invitations:'))!
      expect(back.attributes('href')).toBe('/settings/user')
    })

    it('shows the page title', async () => {
      const wrapper = await mountInvitations()

      expect(wrapper.find('h1').text()).toBe('Server Invitations:')
    })
  })

  describe('invitations table', () => {
    it('requests the first page of the invitations of the user sorted by server', async () => {
      await mountInvitations()

      expect(serverService.getUserServerInvitations).toHaveBeenCalledExactlyOnceWith({
        next: null,
        descending: true,
        pageSize: 10,
        sortBy: 'servername',
      })
    })

    it('shows the invitation columns and an Actions column', async () => {
      const wrapper = await mountInvitations()

      expect(wrapper.findAll('th').map((th) => th.text())).toEqual([
        expect.stringContaining('Server'),
        'Invited By',
        'Invited Date',
        'Actions',
      ])
    })

    it('shows each invitation with its formatted date', async () => {
      const wrapper = await mountInvitations()

      const [server, invitedBy, invitedDate] = cells(wrapper, 0).map((td) => td.text())
      expect(server).toBe('Server 1')
      expect(invitedBy).toBe('user-1')
      expect(invitedDate).toBe(dateTimeMapper('2026-11-05T10:42:00Z'))
    })

    it('shows the Join and Reject buttons in each row', async () => {
      const wrapper = await mountInvitations()

      for (const index of [0, 1]) {
        expect(button(wrapper, index, 'Join')).toBeDefined()
        expect(button(wrapper, index, 'Reject')).toBeDefined()
      }
    })
  })

  describe('join', () => {
    it('accepts the invitation of its row', async () => {
      const wrapper = await mountInvitations()

      await click(wrapper, 1, 'Join')

      expect(serverService.updateServerInvitation).toHaveBeenCalledExactlyOnceWith({
        id: 'invitation-2',
        status: InvitationStatus.Accepted,
      })
    })

    it('reloads the app so the new server appears in the list', async () => {
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Join')

      expect(go).toHaveBeenCalledExactlyOnceWith(0)
      // The page is reloaded, so the table does not request the invitations again
      expect(serverService.getUserServerInvitations).toHaveBeenCalledTimes(1)
    })

    it('does not reload the app when the request fails', async () => {
      serverService.updateServerInvitation.mockRejectedValue(new Error('Network down'))
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Join')

      expect(go).not.toHaveBeenCalled()
      expect(rows(wrapper)).toHaveLength(2)
      expect(button(wrapper, 0, 'Join').attributes('disabled')).toBeUndefined()
    })
  })

  describe('reject', () => {
    it('rejects the invitation of its row', async () => {
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Reject')

      expect(serverService.updateServerInvitation).toHaveBeenCalledExactlyOnceWith({
        id: 'invitation-1',
        status: InvitationStatus.Rejected,
      })
      expect(go).not.toHaveBeenCalled()
    })

    it('reloads the current page of the table', async () => {
      serverService.getUserServerInvitations
        .mockResolvedValueOnce(page([invitation(1), invitation(2)]))
        .mockResolvedValueOnce(page([invitation(2)]))
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Reject')

      expect(serverService.getUserServerInvitations).toHaveBeenCalledTimes(2)
      expect(serverService.getUserServerInvitations).toHaveBeenLastCalledWith(
        expect.objectContaining({ next: null })
      )
      expect(cells(wrapper, 0).map((td) => td.text())[0]).toBe('Server 2')
      expect(rows(wrapper)).toHaveLength(1)
    })

    it('reloads the page it is on, not the first one', async () => {
      serverService.getUserServerInvitations
        .mockResolvedValueOnce(page([invitation(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([invitation(11)], null, 15))
        .mockResolvedValueOnce(page([invitation(12)], null, 15))
      const wrapper = await mountInvitations()
      await wrapper
        .findAll('button')
        .find((b) => b.text() === 'chevron_right')!
        .trigger('click')
      await flushPromises()

      await click(wrapper, 0, 'Reject')

      expect(serverService.getUserServerInvitations).toHaveBeenLastCalledWith(
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(cells(wrapper, 0)[0]!.text()).toBe('Server 12')
    })

    it('keeps the invitation and does not reload the table when the request fails', async () => {
      serverService.updateServerInvitation.mockRejectedValue(new Error('Network down'))
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Reject')

      expect(serverService.getUserServerInvitations).toHaveBeenCalledTimes(1)
      expect(rows(wrapper)).toHaveLength(2)
    })
  })

  describe('while an invitation is being updated', () => {
    it('disables both buttons of its row only', async () => {
      const request = deferred<string | undefined>()
      serverService.updateServerInvitation.mockReturnValue(request.promise)
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Reject')

      expect(button(wrapper, 0, 'Join').attributes('disabled')).toBeDefined()
      expect(button(wrapper, 0, 'Reject').attributes('disabled')).toBeDefined()
      expect(button(wrapper, 1, 'Join').attributes('disabled')).toBeUndefined()

      request.resolve(undefined)
      await flushPromises()
      expect(button(wrapper, 0, 'Reject').attributes('disabled')).toBeUndefined()
    })

    it('ignores a second click on the same row', async () => {
      const request = deferred<string | undefined>()
      serverService.updateServerInvitation.mockReturnValue(request.promise)
      const wrapper = await mountInvitations()

      // Calls the handler twice even if the DOM disables the button in between
      await button(wrapper, 0, 'Reject').trigger('click')
      button(wrapper, 0, 'Join').element.removeAttribute('disabled')
      await button(wrapper, 0, 'Join').trigger('click')
      request.resolve(undefined)
      await flushPromises()

      expect(serverService.updateServerInvitation).toHaveBeenCalledTimes(1)
      expect(go).not.toHaveBeenCalled()
    })

    it('lets another row be updated at the same time', async () => {
      serverService.updateServerInvitation.mockReturnValue(new Promise(() => {}))
      const wrapper = await mountInvitations()

      await click(wrapper, 0, 'Reject')
      await click(wrapper, 1, 'Reject')

      expect(serverService.updateServerInvitation).toHaveBeenCalledTimes(2)
    })
  })
})
