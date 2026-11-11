import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import { nextTick } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ServerInvitations from '@/components/server/settings/Members/ServerInvitations.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import { dateTimeMapper } from '@/services/common/utils'
import {
  ServerInvitationStatus,
  serverInvitationStatusMapper,
  type ChatServer,
  type PageResult,
  type ServerInvitation,
} from '@/types'

// ---------- mocks ----------

const serverService = vi.hoisted(() => ({
  getServerInvitations: vi.fn(),
  updateServerInvitation: vi.fn(),
}))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: serverService }))

// ---------- helpers ----------

const server = { id: 'server-1', name: 'My Server' } as ChatServer

function invitation(id: number, status = ServerInvitationStatus.Pending): ServerInvitation {
  return {
    id: `invitation-${id}`,
    username: `user-${id}`,
    status,
    createdAt: '2026-11-05T10:42:00Z',
    modifiedAt: '2026-11-06T09:15:00Z',
  } as ServerInvitation
}

function page(
  items: ServerInvitation[],
  next: string | null = null,
  total = items.length
): PageResult<ServerInvitation> {
  return { items, next, total, pageSize: 10 }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

let pinia: Pinia

// Pass null to mount it without a current server
async function mountServerInvitations(currentServer: ChatServer | null = server) {
  if (currentServer) serverInfoStore().setCurrentServerSettings(currentServer)
  const wrapper = mount(ServerInvitations, {
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
  })
  await flushPromises()
  return wrapper
}

const rows = (wrapper: VueWrapper) => wrapper.findAll('tbody tr')
const cells = (wrapper: VueWrapper, row: number) => rows(wrapper)[row]!.findAll('td')
const revokeButton = (wrapper: VueWrapper, row: number) =>
  rows(wrapper)
    [row]!.findAll('button')
    .find((b) => b.text() === 'Revoke')
const nextButton = (wrapper: VueWrapper) =>
  wrapper.findAll('button').find((b) => b.text() === 'chevron_right')

async function clickRevoke(wrapper: VueWrapper, row: number) {
  await revokeButton(wrapper, row)!.trigger('click')
  await flushPromises()
}

// ---------- tests ----------

describe('ServerInvitations.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    serverService.getServerInvitations.mockReset()
    serverService.updateServerInvitation.mockReset()
    serverService.getServerInvitations.mockResolvedValue(
      page([invitation(1), invitation(2, ServerInvitationStatus.Revoked)])
    )
    serverService.updateServerInvitation.mockResolvedValue(undefined)
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('header', () => {
    it('links back to the server members page', async () => {
      const wrapper = await mountServerInvitations()

      const link = wrapper.findComponent(RouterLinkStub)
      expect(link.props('to')).toEqual({ name: 'serverMembers' })
      expect(link.text()).toContain('Server Invitations:')
    })

    it('shows the name of the current server and the page title', async () => {
      const wrapper = await mountServerInvitations()

      expect(wrapper.find('h2').text()).toBe('Server: My Server')
      expect(wrapper.find('h1').text()).toBe('Server Invitations:')
    })
  })

  describe('invitations table', () => {
    it('requests the first page of invitations of the current server sorted by username', async () => {
      await mountServerInvitations()

      expect(serverService.getServerInvitations).toHaveBeenCalledTimes(1)
      expect(serverService.getServerInvitations).toHaveBeenCalledWith('server-1', {
        next: null,
        descending: true,
        pageSize: 10,
        sortBy: 'username',
      })
    })

    it('shows the invitation columns and an Actions column', async () => {
      const wrapper = await mountServerInvitations()

      expect(wrapper.findAll('th').map((th) => th.text())).toEqual([
        expect.stringContaining('Username'),
        'Status',
        'Invited Date',
        'Last Update',
        'Actions',
      ])
    })

    it('shows each invitation with its status name and formatted dates', async () => {
      const wrapper = await mountServerInvitations()

      const [username, status, invitedDate, lastUpdate] = cells(wrapper, 0).map((td) => td.text())
      expect(username).toBe('user-1')
      expect(status).toBe(serverInvitationStatusMapper(ServerInvitationStatus.Pending))
      expect(invitedDate).toBe(dateTimeMapper('2026-11-05T10:42:00Z'))
      expect(lastUpdate).toBe(dateTimeMapper('2026-11-06T09:15:00Z'))
    })

    it('requests the next page of invitations of the current server', async () => {
      serverService.getServerInvitations
        .mockResolvedValueOnce(page([invitation(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([invitation(11)], null, 15))
      const wrapper = await mountServerInvitations()

      await nextButton(wrapper)!.trigger('click')
      await flushPromises()

      expect(serverService.getServerInvitations).toHaveBeenLastCalledWith(
        'server-1',
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(cells(wrapper, 0)[0]!.text()).toBe('user-11')
    })

    it('shows an empty table without requesting anything when there is no current server', async () => {
      const wrapper = await mountServerInvitations(null)

      expect(serverService.getServerInvitations).not.toHaveBeenCalled()
      expect(rows(wrapper)).toHaveLength(0)
    })
  })

  describe('revoke button', () => {
    it('is shown only for pending invitations', async () => {
      serverService.getServerInvitations.mockResolvedValueOnce(
        page([
          invitation(1, ServerInvitationStatus.Pending),
          invitation(2, ServerInvitationStatus.Accepted),
          invitation(3, ServerInvitationStatus.Rejected),
          invitation(4, ServerInvitationStatus.Revoked),
        ])
      )

      const wrapper = await mountServerInvitations()

      expect(rows(wrapper).map((_, index) => revokeButton(wrapper, index) !== undefined)).toEqual([
        true,
        false,
        false,
        false,
      ])
    })

    it('revokes the invitation of its row', async () => {
      serverService.getServerInvitations.mockResolvedValueOnce(page([invitation(1), invitation(2)]))
      const wrapper = await mountServerInvitations()

      await clickRevoke(wrapper, 1)

      expect(serverService.updateServerInvitation).toHaveBeenCalledTimes(1)
      expect(serverService.updateServerInvitation).toHaveBeenCalledWith({
        id: 'invitation-2',
        status: ServerInvitationStatus.Revoked,
      })
    })

    it('reloads the table after revoking, so the new status is shown', async () => {
      serverService.getServerInvitations
        .mockResolvedValueOnce(page([invitation(1)]))
        .mockResolvedValueOnce(page([invitation(1, ServerInvitationStatus.Revoked)]))
      const wrapper = await mountServerInvitations()

      await clickRevoke(wrapper, 0)

      expect(serverService.getServerInvitations).toHaveBeenCalledTimes(2)
      expect(cells(wrapper, 0)[1]!.text()).toBe(
        serverInvitationStatusMapper(ServerInvitationStatus.Revoked)
      )
      expect(revokeButton(wrapper, 0)).toBeUndefined()
    })

    it('reloads the page the user is on', async () => {
      serverService.getServerInvitations
        .mockResolvedValueOnce(page([invitation(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([invitation(11)], null, 15))
        .mockResolvedValueOnce(page([invitation(11, ServerInvitationStatus.Revoked)], null, 15))
      const wrapper = await mountServerInvitations()
      await nextButton(wrapper)!.trigger('click')
      await flushPromises()

      await clickRevoke(wrapper, 0)

      expect(serverService.getServerInvitations).toHaveBeenLastCalledWith(
        'server-1',
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(wrapper.text()).toContain('Page 2 / 2')
    })

    it('is disabled while the invitation is being revoked', async () => {
      const request = deferred<void>()
      serverService.updateServerInvitation.mockReturnValueOnce(request.promise)
      const wrapper = await mountServerInvitations()

      await revokeButton(wrapper, 0)!.trigger('click')
      await nextTick()
      expect(revokeButton(wrapper, 0)!.attributes('disabled')).toBeDefined()

      serverService.getServerInvitations.mockResolvedValueOnce(page([invitation(1)]))
      request.resolve()
      await flushPromises()
      expect(revokeButton(wrapper, 0)!.attributes('disabled')).toBeUndefined()
    })

    it('does not revoke twice on a double click', async () => {
      const request = deferred<void>()
      serverService.updateServerInvitation.mockReturnValueOnce(request.promise)
      const wrapper = await mountServerInvitations()

      const button = revokeButton(wrapper, 0)!
      // Both clicks happen before Vue re-renders the button as disabled,
      // so this checks the guard in revokeInvitation and not only the disabled attribute
      void button.trigger('click')
      void button.trigger('click')
      request.resolve()
      await flushPromises()

      expect(serverService.updateServerInvitation).toHaveBeenCalledTimes(1)
    })

    it('only disables the button of the invitation being revoked', async () => {
      const request = deferred<void>()
      serverService.updateServerInvitation.mockReturnValueOnce(request.promise)
      serverService.getServerInvitations.mockResolvedValueOnce(page([invitation(1), invitation(2)]))
      const wrapper = await mountServerInvitations()

      await revokeButton(wrapper, 0)!.trigger('click')
      await nextTick()

      expect(revokeButton(wrapper, 1)!.attributes('disabled')).toBeUndefined()

      request.resolve()
      await flushPromises()
    })

    it('keeps the invitation and does not reload when revoking fails', async () => {
      serverService.updateServerInvitation.mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountServerInvitations()

      await clickRevoke(wrapper, 0)

      expect(serverService.getServerInvitations).toHaveBeenCalledTimes(1)
      expect(cells(wrapper, 0)[1]!.text()).toBe(
        serverInvitationStatusMapper(ServerInvitationStatus.Pending)
      )
      expect(revokeButton(wrapper, 0)!.attributes('disabled')).toBeUndefined()
    })

    it('can try again after a failed revoke', async () => {
      serverService.updateServerInvitation.mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountServerInvitations()
      await clickRevoke(wrapper, 0)

      await clickRevoke(wrapper, 0)

      expect(serverService.updateServerInvitation).toHaveBeenCalledTimes(2)
    })

    it('keeps the current rows when the reload after revoking fails', async () => {
      serverService.getServerInvitations
        .mockResolvedValueOnce(page([invitation(1)]))
        .mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountServerInvitations()

      await clickRevoke(wrapper, 0)

      expect(rows(wrapper)).toHaveLength(1)
      expect(revokeButton(wrapper, 0)!.attributes('disabled')).toBeUndefined()
    })
  })
})
