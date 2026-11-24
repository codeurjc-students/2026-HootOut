import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { serverMembersStore, OFFLINE_PAGE_SIZE } from '@/stores/serverMembers'
import type { ServerPresenceEvent } from '@/stores/presence'
import {
  PresenceStatus,
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

const presence = (
  userId: string,
  status: PresenceStatus,
  serverId = SERVER
): ServerPresenceEvent => ({
  type: 'presence',
  serverId,
  userId,
  status,
})
const goesOnline = (name: string, serverId = SERVER) =>
  presence(`user-${name}`, PresenceStatus.Online, serverId)
const goesOffline = (name: string) => presence(`user-${name}`, PresenceStatus.Offline)
const snapshot = (names: string[], serverId = SERVER): ServerPresenceEvent => ({
  type: 'snapshot',
  serverId,
  onlineUserIds: names.map((n) => `user-${n}`),
})

// A promise resolved from the test, to control when the API answers
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (error: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

const names = (members: ServerMember[]) => members.map((m) => m.username)

// ---------- tests ----------

describe('serverMembers store', () => {
  let store: ReturnType<typeof serverMembersStore>

  beforeEach(() => {
    setActivePinia(createPinia())
    store = serverMembersStore()
    serverService.getOnlineServerMembers.mockReset()
    serverService.getOfflineServerMembers.mockReset()
    serverService.getOnlineServerMembers.mockResolvedValue(online([member('bob'), member('ana')]))
    serverService.getOfflineServerMembers.mockResolvedValue(page([member('carl'), member('dave')]))
  })

  // load() + the first page of offline members, as the view does (InfiniteScrollList asks for it)
  async function loadWithFirstPage(id = SERVER) {
    await store.load(id)
    return store.loadOfflinePage(null)
  }

  describe('load', () => {
    it('requests only the online members', async () => {
      await store.load(SERVER)

      expect(serverService.getOnlineServerMembers).toHaveBeenCalledExactlyOnceWith(SERVER)
      expect(serverService.getOfflineServerMembers).not.toHaveBeenCalled()
    })

    it('fills the online list sorted by username, with its count', async () => {
      await store.load(SERVER)

      expect(names(store.onlineMembers)).toEqual(['ana', 'bob'])
      expect(store.onlineCount).toBe(2)
      expect(store.loading).toBe(false)
      expect(store.error).toBe(false)
    })

    it('does not know the offline count until the first offline page arrives', async () => {
      await store.load(SERVER)

      expect(store.offlineCount).toBeNull()
      expect(store.offlineMembers).toEqual([])
    })

    it('sorts ignoring case and accents', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('Zeta'), member('beta'), member('Álvaro'), member('alfa')])
      )

      await store.load(SERVER)

      expect(names(store.onlineMembers)).toEqual(['alfa', 'Álvaro', 'beta', 'Zeta'])
    })

    it('is loading until the online members arrive', async () => {
      const answer = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValue(answer.promise)

      const loading = store.load(SERVER)
      expect(store.loading).toBe(true)

      answer.resolve(online([]))
      await loading
      expect(store.loading).toBe(false)
    })

    it('shows an error when the request fails', async () => {
      serverService.getOnlineServerMembers.mockRejectedValue(new Error('500'))

      await store.load(SERVER)

      expect(store.error).toBe(true)
      expect(store.loading).toBe(false)
      expect(store.onlineMembers).toEqual([])
    })

    it('forgets everything without a server', async () => {
      await loadWithFirstPage()

      await store.load(undefined)

      expect(store.serverId).toBeUndefined()
      expect(store.onlineMembers).toEqual([])
      expect(store.offlineMembers).toEqual([])
      expect(store.onlineCount).toBe(0)
      expect(store.offlineCount).toBeNull()
    })

    it('changes the version of the offline list on every load', async () => {
      const before = store.offlineListVersion

      await store.load(SERVER)
      await store.load(SERVER)

      expect(store.offlineListVersion).toBe(before + 2)
    })

    it('ignores the answer of a previous server', async () => {
      const first = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValueOnce(first.promise)
      const loadingFirst = store.load(SERVER)

      serverService.getOnlineServerMembers.mockResolvedValue(online([member('zoe')]))
      await store.load(OTHER_SERVER)
      first.resolve(online([member('ana')]))
      await loadingFirst

      expect(store.serverId).toBe(OTHER_SERVER)
      expect(names(store.onlineMembers)).toEqual(['zoe'])
      expect(store.loading).toBe(false)
    })

    it('normalizes the server id', async () => {
      await store.load(SERVER.toUpperCase())

      store.handlePresenceEvent(goesOffline('ana'))

      expect(store.onlineCount).toBe(1)
    })
  })

  describe('loadOfflinePage', () => {
    it('requests the page sorted by username with the cursor it receives', async () => {
      await store.load(SERVER)

      await store.loadOfflinePage(null)
      await store.loadOfflinePage('member-dave')

      expect(serverService.getOfflineServerMembers.mock.calls).toEqual([
        [
          SERVER,
          { next: null, pageSize: OFFLINE_PAGE_SIZE, sortBy: 'username', descending: false },
        ],
        [
          SERVER,
          {
            next: 'member-dave',
            pageSize: OFFLINE_PAGE_SIZE,
            sortBy: 'username',
            descending: false,
          },
        ],
      ])
    })

    it('returns the cursor of the next page, null on the last one', async () => {
      await store.load(SERVER)
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('carl')], 'member-carl', 2)
      )
      serverService.getOfflineServerMembers.mockResolvedValueOnce(page([member('dave')], null, 2))

      expect(await store.loadOfflinePage(null)).toBe('member-carl')
      expect(await store.loadOfflinePage('member-carl')).toBeNull()
    })

    it('adds the members sorted by username and the offline count', async () => {
      expect(await loadWithFirstPage()).toBeNull()

      expect(names(store.offlineMembers)).toEqual(['carl', 'dave'])
      expect(store.offlineCount).toBe(2)
    })

    it('uses the total of the API for the offline count', async () => {
      serverService.getOfflineServerMembers.mockResolvedValue(
        page([member('carl')], 'member-carl', 120)
      )

      await loadWithFirstPage()

      expect(store.offlineCount).toBe(120)
    })

    it('adds the online members to the count when the page arrives before them', async () => {
      const onlineAnswer = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValueOnce(onlineAnswer.promise)
      serverService.getOfflineServerMembers.mockResolvedValue(
        page([member('carl')], 'member-carl', 10)
      )
      const loading = store.load(SERVER)

      await store.loadOfflinePage(null)
      onlineAnswer.resolve(online([member('ana'), member('bob')]))
      await loading

      expect(store.onlineCount).toBe(2)
      expect(store.offlineCount).toBe(10)
    })

    it('does not repeat a member that is already in the list', async () => {
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('carl'), member('dave')], 'member-dave', 3)
      )
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('dave'), member('eve')], null, 3)
      )
      await loadWithFirstPage()

      await store.loadOfflinePage('member-dave')

      expect(names(store.offlineMembers)).toEqual(['carl', 'dave', 'eve'])
    })

    it('stops the list and shows an error when the request fails', async () => {
      await store.load(SERVER)
      serverService.getOfflineServerMembers.mockRejectedValueOnce(new Error('500'))

      expect(await store.loadOfflinePage(null)).toBeNull()

      expect(store.offlineError).toBe(true)
      expect(store.error).toBe(false)
      expect(names(store.onlineMembers)).toEqual(['ana', 'bob'])
    })

    it('ignores the page of a previous server and stops its list', async () => {
      await store.load(SERVER)
      const next = deferred<PageResult<ServerMember>>()
      serverService.getOfflineServerMembers.mockReturnValueOnce(next.promise)
      const loadingPage = store.loadOfflinePage(null)

      serverService.getOnlineServerMembers.mockResolvedValue(online([]))
      await store.load(OTHER_SERVER)
      next.resolve(page([member('carl')], 'member-carl', 5))

      expect(await loadingPage).toBeNull()
      expect(store.offlineMembers).toEqual([])
      expect(store.offlineCount).toBeNull()
    })

    it('requests nothing without a server', async () => {
      expect(await store.loadOfflinePage(null)).toBeNull()

      expect(serverService.getOfflineServerMembers).not.toHaveBeenCalled()
    })
  })

  describe('retryOffline', () => {
    it('clears the error and changes the version, so the list starts again', async () => {
      await store.load(SERVER)
      serverService.getOfflineServerMembers.mockRejectedValueOnce(new Error('500'))
      await store.loadOfflinePage(null)
      const version = store.offlineListVersion

      store.retryOffline()

      expect(store.offlineError).toBe(false)
      expect(store.offlineListVersion).toBe(version + 1)
      expect(await store.loadOfflinePage(null)).toBeNull()
      expect(names(store.offlineMembers)).toEqual(['carl', 'dave'])
    })
  })

  describe('presence events', () => {
    it('moves a member that goes offline to the offline list', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOffline('bob'))

      expect(names(store.onlineMembers)).toEqual(['ana'])
      expect(names(store.offlineMembers)).toEqual(['bob', 'carl', 'dave'])
      expect(store.onlineCount).toBe(1)
      expect(store.offlineCount).toBe(3)
    })

    it('moves a loaded offline member that goes online to the online list', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOnline('dave'))

      expect(names(store.onlineMembers)).toEqual(['ana', 'bob', 'dave'])
      expect(names(store.offlineMembers)).toEqual(['carl'])
      expect(store.onlineCount).toBe(3)
      expect(store.offlineCount).toBe(1)
      expect(serverService.getOnlineServerMembers).toHaveBeenCalledOnce()
    })

    it('treats a repeated online or offline as a no-op', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOnline('ana'))
      store.handlePresenceEvent(goesOffline('carl'))

      expect(names(store.onlineMembers)).toEqual(['ana', 'bob'])
      expect(names(store.offlineMembers)).toEqual(['carl', 'dave'])
      expect(store.onlineCount).toBe(2)
      expect(serverService.getOnlineServerMembers).toHaveBeenCalledOnce()
    })

    it('requests the online members again for a member that was not loaded', async () => {
      await loadWithFirstPage()
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob'), member('zoe')])
      )

      store.handlePresenceEvent(goesOnline('zoe'))
      expect(store.onlineCount).toBe(3)
      await vi.waitFor(() => expect(names(store.onlineMembers)).toEqual(['ana', 'bob', 'zoe']))

      expect(serverService.getOnlineServerMembers).toHaveBeenCalledTimes(2)
      expect(store.offlineCount).toBe(1)
    })

    it('decides who is online with the events, not with the refreshed list', async () => {
      await loadWithFirstPage()
      const refresh = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValueOnce(refresh.promise)

      store.handlePresenceEvent(goesOnline('zoe'))
      // While the request runs ana goes offline, but the answer is older and still has her
      store.handlePresenceEvent(goesOffline('ana'))
      refresh.resolve(online([member('ana'), member('bob'), member('zoe')]))
      await vi.waitFor(() => expect(names(store.onlineMembers)).toEqual(['bob', 'zoe']))

      expect(names(store.offlineMembers)).toContain('ana')
    })

    it('refreshes once more when another unknown member arrives during a refresh', async () => {
      await loadWithFirstPage()
      const refresh = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValueOnce(refresh.promise)
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob'), member('yan'), member('zoe')])
      )

      store.handlePresenceEvent(goesOnline('zoe'))
      store.handlePresenceEvent(goesOnline('yan'))
      store.handlePresenceEvent(goesOnline('xia'))
      refresh.resolve(online([member('ana'), member('bob'), member('zoe')]))
      await vi.waitFor(() =>
        expect(names(store.onlineMembers)).toEqual(['ana', 'bob', 'yan', 'zoe'])
      )

      // The first refresh plus only one more for yan and xia
      expect(serverService.getOnlineServerMembers).toHaveBeenCalledTimes(3)
      expect(store.onlineCount).toBe(5)
      expect(store.hiddenOnlineCount).toBe(1)
    })

    it('does not request the list again when the backend capped it', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob')], 250)
      )

      await loadWithFirstPage()
      store.handlePresenceEvent(goesOnline('zoe'))

      expect(serverService.getOnlineServerMembers).toHaveBeenCalledOnce()
      // Counted, but not listed
      expect(store.onlineCount).toBe(251)
      expect(store.hiddenOnlineCount).toBe(249)
    })

    it('shows how many online members are not listed when the backend capped the list', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob')], 250)
      )

      await loadWithFirstPage()

      expect(store.hiddenOnlineCount).toBe(248)
    })

    it('does not show a member that went offline after the last page loaded until its page arrives', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(online([member('ana'), member('zoe')]))
      serverService.getOfflineServerMembers.mockResolvedValueOnce(
        page([member('carl'), member('dave')], 'member-dave', 10)
      )
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOffline('zoe'))
      store.handlePresenceEvent(goesOffline('ana'))

      // ana sorts before dave and is shown; zoe comes later in its page
      expect(names(store.offlineMembers)).toEqual(['ana', 'carl', 'dave'])
      expect(store.offlineCount).toBe(12)
    })

    it('shows a member that went offline anywhere when every page is loaded', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(online([member('zoe')]))
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOffline('zoe'))

      expect(names(store.offlineMembers)).toEqual(['carl', 'dave', 'zoe'])
    })

    it('ignores the events of another server', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOnline('carl', OTHER_SERVER))
      store.handlePresenceEvent(snapshot([], OTHER_SERVER))

      expect(names(store.onlineMembers)).toEqual(['ana', 'bob'])
    })

    it('ignores the events without a server', () => {
      store.handlePresenceEvent(goesOnline('carl'))

      expect(store.onlineMembers).toEqual([])
      expect(serverService.getOnlineServerMembers).not.toHaveBeenCalled()
    })

    it('ignores the events after a failed load', async () => {
      serverService.getOnlineServerMembers.mockRejectedValue(new Error('500'))
      await loadWithFirstPage()

      store.handlePresenceEvent(goesOnline('carl'))

      expect(store.onlineMembers).toEqual([])
      expect(serverService.getOnlineServerMembers).toHaveBeenCalledOnce()
    })

    it('applies the events received while loading after the answer', async () => {
      const onlineAnswer = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValueOnce(onlineAnswer.promise)
      const loading = store.load(SERVER)

      store.handlePresenceEvent(goesOffline('bob'))
      store.handlePresenceEvent(goesOnline('carl'))
      onlineAnswer.resolve(online([member('ana'), member('bob')]))
      await loading
      // carl's username arrives with the offline page (read before it went online)
      await store.loadOfflinePage(null)

      expect(names(store.onlineMembers)).toEqual(['ana', 'carl'])
      expect(names(store.offlineMembers)).toEqual(['bob', 'dave'])
    })
  })

  describe('snapshot', () => {
    it('replaces who is online', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(snapshot(['bob', 'carl']))

      expect(names(store.onlineMembers)).toEqual(['bob', 'carl'])
      expect(names(store.offlineMembers)).toEqual(['ana', 'dave'])
      expect(store.onlineCount).toBe(2)
      expect(store.offlineCount).toBe(2)
    })

    it('counts every online member of the snapshot, even if the API capped the list', async () => {
      serverService.getOnlineServerMembers.mockResolvedValue(
        online([member('ana'), member('bob')], 3)
      )
      await loadWithFirstPage()

      store.handlePresenceEvent(snapshot(['ana', 'bob', 'zoe', 'yan']))

      expect(store.onlineCount).toBe(4)
      expect(store.hiddenOnlineCount).toBe(2)
    })

    it('received while loading wins over the answer of the API', async () => {
      const onlineAnswer = deferred<OnlineServerMembers>()
      serverService.getOnlineServerMembers.mockReturnValueOnce(onlineAnswer.promise)
      const loading = store.load(SERVER)

      store.handlePresenceEvent(snapshot(['dave']))
      onlineAnswer.resolve(online([member('ana'), member('bob')]))
      await loading
      await store.loadOfflinePage(null)

      expect(names(store.onlineMembers)).toEqual(['dave'])
      expect(names(store.offlineMembers)).toEqual(['ana', 'bob', 'carl'])
    })

    it('requests the usernames of the online members that are not loaded', async () => {
      await loadWithFirstPage()
      serverService.getOnlineServerMembers.mockResolvedValue(online([member('zoe')]))

      store.handlePresenceEvent(snapshot(['zoe']))
      await vi.waitFor(() => expect(names(store.onlineMembers)).toEqual(['zoe']))

      expect(serverService.getOnlineServerMembers).toHaveBeenCalledTimes(2)
    })

    it('does not request anything when every online member is loaded', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(snapshot(['ana']))

      expect(serverService.getOnlineServerMembers).toHaveBeenCalledOnce()
    })

    it('empty: everybody is offline', async () => {
      await loadWithFirstPage()

      store.handlePresenceEvent(snapshot([]))

      expect(store.onlineMembers).toEqual([])
      expect(store.onlineCount).toBe(0)
      expect(store.offlineCount).toBe(4)
    })
  })
})
