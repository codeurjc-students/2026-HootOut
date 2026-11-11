import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ActiveMembers from '@/components/server/settings/Members/ActiveMembers.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import { dateTimeMapper } from '@/services/common/utils'
import type { ChatServer, PageResult, ServerMember } from '@/types'

// ---------- mocks ----------

const serverService = vi.hoisted(() => ({ getServerMembers: vi.fn() }))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: serverService }))

// ---------- helpers ----------

const server = { id: 'server-1', name: 'My Server' } as ChatServer

function member(id: number, createdAt = '2026-11-05T10:42:00Z'): ServerMember {
  return { id: `member-${id}`, username: `user-${id}`, createdAt } as ServerMember
}

function page(
  items: ServerMember[],
  next: string | null = null,
  total = items.length
): PageResult<ServerMember> {
  return { items, next, total, pageSize: 10 }
}

let pinia: Pinia

// Pass null to mount it without a current server
async function mountActiveMembers(currentServer: ChatServer | null = server) {
  if (currentServer) serverInfoStore().setCurrentServerSettings(currentServer)
  const wrapper = mount(ActiveMembers, {
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
  })
  await flushPromises()
  return wrapper
}

const rows = (wrapper: VueWrapper) => wrapper.findAll('tbody tr')
const headerCell = (wrapper: VueWrapper, name: string) =>
  wrapper.findAll('th').find((th) => th.text().includes(name))!
const nextButton = (wrapper: VueWrapper) =>
  wrapper.findAll('button').find((b) => b.text() === 'chevron_right')

// ---------- tests ----------

describe('ActiveMembers.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    serverService.getServerMembers.mockReset()
    serverService.getServerMembers.mockResolvedValue(page([member(1), member(2)]))
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('header', () => {
    it('links back to the server members page', async () => {
      const wrapper = await mountActiveMembers()

      const link = wrapper.findComponent(RouterLinkStub)
      expect(link.props('to')).toEqual({ name: 'serverMembers' })
      expect(link.text()).toContain('Active Members:')
    })

    it('shows the name of the current server', async () => {
      const wrapper = await mountActiveMembers()

      expect(wrapper.find('h2').text()).toBe('Server: My Server')
    })

    it('shows the page title', async () => {
      const wrapper = await mountActiveMembers()

      expect(wrapper.find('h1').text()).toBe('Active Members:')
    })
  })

  describe('members table', () => {
    it('requests the first page of members of the current server sorted by username', async () => {
      await mountActiveMembers()

      expect(serverService.getServerMembers).toHaveBeenCalledTimes(1)
      expect(serverService.getServerMembers).toHaveBeenCalledWith('server-1', {
        next: null,
        descending: true,
        pageSize: 10,
        sortBy: 'username',
      })
    })

    it('shows the Username and Joined At columns, without actions', async () => {
      const wrapper = await mountActiveMembers()

      expect(wrapper.findAll('th').map((th) => th.text())).toEqual([
        expect.stringContaining('Username'),
        'Joined At',
      ])
    })

    it('shows one row per member with its username', async () => {
      const wrapper = await mountActiveMembers()

      expect(rows(wrapper)).toHaveLength(2)
      expect(rows(wrapper)[0]!.findAll('td')[0]!.text()).toBe('user-1')
      expect(rows(wrapper)[1]!.findAll('td')[0]!.text()).toBe('user-2')
    })

    it('formats the join date with dateTimeMapper', async () => {
      serverService.getServerMembers.mockResolvedValueOnce(
        page([member(1, '2026-11-05T10:42:00Z')])
      )

      const wrapper = await mountActiveMembers()

      const joinedAt = rows(wrapper)[0]!.findAll('td')[1]!.text()
      expect(joinedAt).toBe(dateTimeMapper('2026-11-05T10:42:00Z'))
      expect(joinedAt).not.toContain('2026-11-05T')
    })

    it('shows an empty table when the server has no members', async () => {
      serverService.getServerMembers.mockResolvedValueOnce(page([]))

      const wrapper = await mountActiveMembers()

      expect(rows(wrapper)).toHaveLength(0)
      expect(wrapper.text()).toContain('Page 1 / 1')
    })

    it('requests the members sorted by join date when its header is clicked', async () => {
      const wrapper = await mountActiveMembers()

      await headerCell(wrapper, 'Joined At').trigger('click')
      await flushPromises()

      expect(serverService.getServerMembers).toHaveBeenLastCalledWith(
        'server-1',
        expect.objectContaining({ sortBy: 'createdat', descending: true, next: null })
      )
    })

    it('requests the next page of members of the current server', async () => {
      serverService.getServerMembers
        .mockResolvedValueOnce(page([member(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([member(11)], null, 15))
      const wrapper = await mountActiveMembers()

      await nextButton(wrapper)!.trigger('click')
      await flushPromises()

      expect(serverService.getServerMembers).toHaveBeenLastCalledWith(
        'server-1',
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(rows(wrapper)[0]!.text()).toContain('user-11')
      expect(wrapper.text()).toContain('Page 2 / 2')
    })
  })

  describe('without a current server', () => {
    // Should not happen: the route guard sets the server before entering the page
    it('shows an empty table without requesting anything', async () => {
      const wrapper = await mountActiveMembers(null)

      expect(serverService.getServerMembers).not.toHaveBeenCalled()
      expect(rows(wrapper)).toHaveLength(0)
      expect(wrapper.find('h2').text()).toBe('Server:')
    })
  })
})
