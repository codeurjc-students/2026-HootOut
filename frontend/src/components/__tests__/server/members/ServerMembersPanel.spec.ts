import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, type PropType } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ServerMembersPanel from '@/components/server/members/ServerMembersPanel.vue'
import { presenceStore } from '@/stores/presence'
import { OFFLINE_PAGE_SIZE } from '@/stores/serverMembers'
import {
  PresenceStatus,
  WSMessageType,
  type OnlineServerMembers,
  type PageResult,
  type ServerMember,
} from '@/types'

// ---------- mocks ----------

const serverService = vi.hoisted(() => ({
  getOnlineServerMembers: vi.fn(),
  getOfflineServerMembers: vi.fn(),
}))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: serverService }))

// ---------- helpers ----------

const SERVER = 'cccccccc-0000-0000-0000-000000000001'
const OTHER_SERVER = 'cccccccc-0000-0000-0000-000000000002'

function member(name: string): ServerMember {
  return {
    id: `member-${name}`,
    userId: `user-${name}`,
    username: name,
    createdAt: '2026-11-05T10:42:00Z',
  }
}

function online(items: ServerMember[], total = items.length): OnlineServerMembers {
  return { items, total }
}

function page(
  items: ServerMember[],
  next: string | null = null,
  total = items.length
): PageResult<ServerMember> {
  return { items, next, total, pageSize: OFFLINE_PAGE_SIZE }
}

let pinia: Pinia

// InfiniteScrollList has its own tests: the stub renders the items with the slot and keeps the
// loadMore it receives, so a test can ask for a page as the list does when its end is visible
type LoadMore = (next: string | null) => Promise<string | null>
const infiniteLists: { loadMore: LoadMore; next: string | null }[] = []
const InfiniteScrollListStub = defineComponent({
  name: 'InfiniteScrollList',
  props: {
    items: { type: Array as PropType<unknown[]>, required: true },
    loadMore: { type: Function as PropType<LoadMore>, required: true },
  },
  setup(props, { slots }) {
    infiniteLists.push({ loadMore: props.loadMore, next: null })
    return () =>
      h(
        'ul',
        { class: 'infinite-scroll-stub' },
        props.items.map((item) => h('li', slots['scroll-item']?.({ item })))
      )
  },
})

// The next page of the list mounted last, with the cursor of the previous one
async function scrollToEnd() {
  const list = infiniteLists.at(-1)!
  list.next = await list.loadMore(list.next)
  await flushPromises()
  return list.next
}

async function mountPanel(serverId: string | null = SERVER, firstPage = true) {
  const wrapper = mount(ServerMembersPanel, {
    props: { serverId: serverId ?? undefined },
    global: { plugins: [pinia], stubs: { InfiniteScrollList: InfiniteScrollListStub } },
  })
  await flushPromises()
  // The list asks for the first page as soon as it is visible
  if (firstPage && infiniteLists.length) await scrollToEnd()
  return wrapper
}

const onlineNames = (w: VueWrapper) => w.findAll('#online-members li').map((li) => li.text())
const offlineNames = (w: VueWrapper) =>
  w.findAll('#offline-members [data-member-status="offline"]').map((m) => m.text())
const onlineTitle = (w: VueWrapper) => w.find('#online-members-title').text()
const offlineTitle = (w: VueWrapper) => w.find('#offline-members-title').text()

// The presence of the members as the websocket sends it (PresenceSync sets the current server)
function receivePresence(name: string, status: PresenceStatus, serverId = SERVER) {
  presenceStore().handleMessage({
    type: WSMessageType.Presence,
    channel: `presence.server.${serverId}`,
    payload: JSON.stringify({ UserId: `user-${name}`, Status: status }),
  })
}

function receiveSnapshot(names: string[]) {
  presenceStore().handleMessage({
    type: WSMessageType.PresenceSnapshot,
    channel: SERVER,
    payload: JSON.stringify({ OnlineUserIds: names.map((n) => `user-${n}`) }),
  })
}

// ---------- tests ----------

describe('ServerMembersPanel.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    presenceStore().setCurrentServer(SERVER)
    infiniteLists.length = 0
    serverService.getOnlineServerMembers.mockReset()
    serverService.getOfflineServerMembers.mockReset()
    serverService.getOnlineServerMembers.mockResolvedValue(online([member('bob'), member('ana')]))
    serverService.getOfflineServerMembers.mockResolvedValue(page([member('carl'), member('dave')]))
  })

  describe('lists', () => {
    it('shows the online and offline members with their counts', async () => {
      const wrapper = await mountPanel()

      expect(onlineTitle(wrapper)).toBe('Online — 2')
      expect(onlineNames(wrapper)).toEqual(['ana', 'bob'])
      expect(offlineTitle(wrapper)).toBe('Offline — 2')
      expect(offlineNames(wrapper)).toEqual(['carl', 'dave'])
    })

    it('is a landmark with an accessible name', async () => {
      const wrapper = await mountPanel()

      expect(wrapper.find('aside').attributes('aria-label')).toBe('Server members')
    })

    it('marks each member with its status', async () => {
      const wrapper = await mountPanel()

      expect(wrapper.findAll('[data-member-status="online"]')).toHaveLength(2)
      expect(wrapper.findAll('[data-member-status="offline"]')).toHaveLength(2)
    })

    it('shows the indicator with the presence of the websocket', async () => {
      const wrapper = await mountPanel()
      receiveSnapshot(['ana', 'bob'])
      await flushPromises()

      const indicators = wrapper
        .findAll('#online-members [data-presence]')
        .map((i) => i.attributes('data-presence'))
      expect(indicators).toEqual(['online', 'online'])
      expect(wrapper.find('#offline-members [data-presence]').attributes('data-presence')).toBe(
        'offline'
      )
    })

    it('shows how many online members are not listed when the backend capped the list', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob')], 205)
      )

      const wrapper = await mountPanel()

      expect(onlineTitle(wrapper)).toBe('Online — 205')
      expect(wrapper.text()).toContain('and 203 more')
    })

    it('does not show "more" when every online member is listed', async () => {
      const wrapper = await mountPanel()

      expect(wrapper.text()).not.toContain('more')
    })

    it('shows empty lists', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(online([]))
      serverService.getOfflineServerMembers.mockResolvedValue(page([]))

      const wrapper = await mountPanel()

      expect(onlineTitle(wrapper)).toBe('Online — 0')
      expect(offlineTitle(wrapper)).toBe('Offline — 0')
      expect(wrapper.findAll('[data-member-status]')).toHaveLength(0)
    })
  })

  describe('loading and errors', () => {
    it('shows a loading message until the members arrive', async () => {
      let resolve!: (value: OnlineServerMembers) => void
      serverService.getOnlineServerMembers.mockReturnValue(new Promise((r) => (resolve = r)))

      const wrapper = mount(ServerMembersPanel, {
        props: { serverId: SERVER },
        global: { plugins: [pinia], stubs: { InfiniteScrollList: InfiniteScrollListStub } },
      })
      await flushPromises()
      expect(wrapper.find('[role="status"]').text()).toBe('Loading members…')
      expect(wrapper.find('#online-members').exists()).toBe(false)

      resolve(online([]))
      await flushPromises()
      expect(wrapper.find('[role="status"]').exists()).toBe(false)
    })

    it('shows an error with a button to try again', async () => {
      serverService.getOnlineServerMembers.mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountPanel()

      expect(wrapper.find('[role="alert"]').text()).toContain('Could not load the members.')

      await wrapper.find('[role="alert"] button').trigger('click')
      await flushPromises()

      expect(wrapper.find('[role="alert"]').exists()).toBe(false)
      expect(onlineNames(wrapper)).toEqual(['ana', 'bob'])
    })
  })

  describe('server', () => {
    it('loads the members of the server of the prop', async () => {
      await mountPanel()

      expect(serverService.getOnlineServerMembers).toHaveBeenCalledExactlyOnceWith(SERVER)
      expect(serverService.getOfflineServerMembers).toHaveBeenCalledExactlyOnceWith(
        SERVER,
        expect.objectContaining({ next: null })
      )
    })

    it('loads the members again when the server changes', async () => {
      const wrapper = await mountPanel()
      serverService.getOnlineServerMembers.mockResolvedValue(online([member('zoe')]))
      serverService.getOfflineServerMembers.mockResolvedValue(page([]))

      await wrapper.setProps({ serverId: OTHER_SERVER })
      await flushPromises()
      // A new list: it starts again from the first page
      expect(infiniteLists).toHaveLength(2)
      expect(await scrollToEnd()).toBeNull()

      expect(serverService.getOnlineServerMembers).toHaveBeenLastCalledWith(OTHER_SERVER)
      expect(serverService.getOfflineServerMembers).toHaveBeenLastCalledWith(
        OTHER_SERVER,
        expect.objectContaining({ next: null })
      )
      expect(onlineNames(wrapper)).toEqual(['zoe'])
      expect(offlineNames(wrapper)).toEqual([])
    })

    it('requests nothing without a server', async () => {
      const wrapper = await mountPanel(null)

      expect(serverService.getOnlineServerMembers).not.toHaveBeenCalled()
      expect(onlineTitle(wrapper)).toBe('Online — 0')
    })
  })

  describe('presence in real time', () => {
    it('moves a member that goes offline to the offline list', async () => {
      const wrapper = await mountPanel()

      receivePresence('bob', PresenceStatus.Offline)
      await flushPromises()

      expect(onlineNames(wrapper)).toEqual(['ana'])
      expect(offlineNames(wrapper)).toEqual(['bob', 'carl', 'dave'])
      expect(onlineTitle(wrapper)).toBe('Online — 1')
      expect(offlineTitle(wrapper)).toBe('Offline — 3')
    })

    it('moves a member that goes online to the online list', async () => {
      const wrapper = await mountPanel()

      receivePresence('carl', PresenceStatus.Online)
      await flushPromises()

      expect(onlineNames(wrapper)).toEqual(['ana', 'bob', 'carl'])
      expect(offlineNames(wrapper)).toEqual(['dave'])
    })

    it('adds a member that was not loaded when it goes online', async () => {
      const wrapper = await mountPanel()
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob'), member('zoe')])
      )

      receivePresence('zoe', PresenceStatus.Online)
      await flushPromises()

      expect(onlineNames(wrapper)).toEqual(['ana', 'bob', 'zoe'])
    })

    it('applies the snapshot of the server', async () => {
      const wrapper = await mountPanel()

      receiveSnapshot(['dave'])
      await flushPromises()

      expect(onlineNames(wrapper)).toEqual(['dave'])
      expect(offlineNames(wrapper)).toEqual(['ana', 'bob', 'carl'])
    })

    it('ignores the presence of another server', async () => {
      const wrapper = await mountPanel()

      receivePresence('carl', PresenceStatus.Online, OTHER_SERVER)
      await flushPromises()

      expect(onlineNames(wrapper)).toEqual(['ana', 'bob'])
    })

    it('stops listening after unmounting', async () => {
      const wrapper = await mountPanel()
      wrapper.unmount()
      serverService.getOnlineServerMembers.mockClear()

      receivePresence('zoe', PresenceStatus.Online)
      await flushPromises()

      // An unknown member would have requested the list again
      expect(serverService.getOnlineServerMembers).not.toHaveBeenCalled()
    })
  })

  describe('offline members with infinite scroll', () => {
    it('shows the offline title without count until the first page arrives', async () => {
      const wrapper = await mountPanel(SERVER, false)

      expect(offlineTitle(wrapper)).toBe('Offline')
      expect(offlineNames(wrapper)).toEqual([])
      expect(serverService.getOfflineServerMembers).not.toHaveBeenCalled()
    })

    it('loads each page with the cursor of the previous one', async () => {
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('carl'), member('dave')], 'member-dave', 4)
      )
      const wrapper = await mountPanel()
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('eve'), member('fred')], null, 4)
      )

      expect(await scrollToEnd()).toBeNull()

      expect(serverService.getOfflineServerMembers).toHaveBeenLastCalledWith(
        SERVER,
        expect.objectContaining({ next: 'member-dave' })
      )
      expect(offlineNames(wrapper)).toEqual(['carl', 'dave', 'eve', 'fred'])
      expect(offlineTitle(wrapper)).toBe('Offline — 4')
    })

    it('has no "show more" button', async () => {
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('carl')], 'member-carl', 4)
      )
      const wrapper = await mountPanel()

      expect(wrapper.findAll('button')).toHaveLength(0)
    })

    it('shows an error with a button that starts the list again', async () => {
      serverService.getOfflineServerMembers.mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountPanel()

      expect(wrapper.find('#offline-members [role="alert"]').text()).toContain(
        'Could not load the offline members.'
      )
      // The online members are still shown
      expect(onlineNames(wrapper)).toEqual(['ana', 'bob'])

      await wrapper.find('#offline-members [role="alert"] button').trigger('click')
      await flushPromises()
      expect(infiniteLists).toHaveLength(2)
      await scrollToEnd()

      expect(wrapper.find('#offline-members [role="alert"]').exists()).toBe(false)
      expect(offlineNames(wrapper)).toEqual(['carl', 'dave'])
    })
  })
})
