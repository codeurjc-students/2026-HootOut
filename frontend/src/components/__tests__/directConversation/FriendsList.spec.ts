import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import FriendsList from '@/components/directConversation/FriendsList.vue'
import { directConversationStore } from '@/stores/directConversation'
import { presenceStore } from '@/stores/presence'
import { PresenceStatus, WSMessageType, type PageResult, type UserFriendResponse } from '@/types'

// ---------- mocks ----------

const friendsService = vi.hoisted(() => ({ getUserFriends: vi.fn() }))
vi.mock('@/services/friendsService/FriendsService', () => ({ default: friendsService }))

const conversationService = vi.hoisted(() => ({ getDirectConversationById: vi.fn() }))
vi.mock('@/services/conversationService/ConversationService', () => ({
  default: conversationService,
}))

// IntersectionObserver does not exist in jsdom
class FakeIntersectionObserver {
  static instances: FakeIntersectionObserver[] = []

  callback: IntersectionObserverCallback
  observe = vi.fn()
  unobserve = vi.fn()
  disconnect = vi.fn()

  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback
    FakeIntersectionObserver.instances.push(this)
  }

  // Simulates the loader becoming visible
  async trigger() {
    const entries = [{ intersectionRatio: 1 } as IntersectionObserverEntry]
    await this.callback(entries, this as unknown as IntersectionObserver)
    await flushPromises()
  }
}

const observer = () => FakeIntersectionObserver.instances.at(-1)!

// ---------- helpers ----------

function makeFriend(id: string, username = id): UserFriendResponse {
  return {
    friendRelationId: `relation-${id}`,
    friend: { id, username },
    chatConversationId: `chat-${id}`,
    directConversationId: `dc-${id}`,
  }
}

function friendsPage(
  items: UserFriendResponse[],
  next: string | null = null,
): PageResult<UserFriendResponse> {
  return { items, next, total: items.length, pageSize: 20 }
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
let pinia: Pinia

// Same paths as the app's router, with test components and no guards
function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/chat', name: 'chat', component: placeholder },
      {
        path: '/chat/direct/:directConversationId?',
        name: 'directConversation',
        component: placeholder,
      },
    ],
  })
}

async function mountFriendsList(path = '/chat/direct') {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(FriendsList, { global: { plugins: [router, pinia] } })
  await flushPromises()
  return wrapper
}

const links = (wrapper: VueWrapper) => wrapper.findAll('a')
const usernames = (wrapper: VueWrapper) => links(wrapper).map((a) => a.text())
const link = (wrapper: VueWrapper, username: string) =>
  links(wrapper).find((a) => a.text() === username)!
const presenceOf = (wrapper: VueWrapper, username: string) =>
  link(wrapper, username).find('[data-presence]').attributes('data-presence')

// As the websocket sends them: the snapshot after connecting and the events of each friend
function receiveFriendsSnapshot(onlineUserIds: string[]) {
  presenceStore().handleMessage({
    type: WSMessageType.PresenceSnapshot,
    channel: 'friends',
    payload: JSON.stringify({ OnlineUserIds: onlineUserIds }),
  })
}

function receivePresence(userId: string, status: PresenceStatus) {
  presenceStore().handleMessage({
    type: WSMessageType.Presence,
    channel: `presence.user.${userId}`,
    payload: JSON.stringify({ UserId: userId, Status: status }),
  })
}

// ---------- tests ----------

describe('FriendsList.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    FakeIntersectionObserver.instances = []
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver)
    router = createTestRouter()
    pinia = createPinia()
    setActivePinia(pinia)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  describe('loading', () => {
    it('shows nothing until the loader becomes visible', async () => {
      const wrapper = await mountFriendsList()

      expect(friendsService.getUserFriends).not.toHaveBeenCalled()
      expect(links(wrapper)).toHaveLength(0)
    })

    it('requests the first page without a cursor', async () => {
      friendsService.getUserFriends.mockResolvedValue(friendsPage([]))
      await mountFriendsList()

      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenCalledExactlyOnceWith({ next: null })
    })

    it('shows a link with the username of each friend', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]),
      )
      const wrapper = await mountFriendsList()

      await observer().trigger()

      expect(usernames(wrapper)).toEqual(['user1', 'user2'])
    })

    it('adds the next page after the friends already loaded', async () => {
      friendsService.getUserFriends
        .mockResolvedValueOnce(
          friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')], 'cursor-2'),
        )
        .mockResolvedValueOnce(friendsPage([makeFriend('u3', 'user3')]))
      const wrapper = await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenLastCalledWith({ next: 'cursor-2' })
      expect(usernames(wrapper)).toEqual(['user1', 'user2', 'user3'])
    })

    it('does not add a friend that is already in the list', async () => {
      friendsService.getUserFriends
        .mockResolvedValueOnce(friendsPage([makeFriend('u1', 'user1')], 'cursor-2'))
        .mockResolvedValueOnce(friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]))
      const wrapper = await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(usernames(wrapper)).toEqual(['user1', 'user2'])
    })

    it('stops loading when there are no more pages', async () => {
      friendsService.getUserFriends.mockResolvedValue(friendsPage([makeFriend('u1', 'user1')]))
      await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenCalledTimes(1)
      expect(observer().disconnect).toHaveBeenCalled()
    })

    it('treats a page without next as the last page', async () => {
      const { next: _, ...lastPage } = friendsPage([makeFriend('u1', 'user1')])
      friendsService.getUserFriends.mockResolvedValue(lastPage)
      await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenCalledTimes(1)
    })

    it('does not request a page twice while one is loading', async () => {
      const request = deferred<PageResult<UserFriendResponse>>()
      friendsService.getUserFriends.mockReturnValue(request.promise)
      await mountFriendsList()

      const first = observer().trigger()
      const second = observer().trigger()
      request.resolve(friendsPage([makeFriend('u1')]))
      await Promise.all([first, second])

      expect(friendsService.getUserFriends).toHaveBeenCalledTimes(1)
    })

    it('keeps the loaded friends and retries the same page after an error', async () => {
      friendsService.getUserFriends
        .mockResolvedValueOnce(friendsPage([makeFriend('u1', 'user1')], 'cursor-2'))
        .mockRejectedValueOnce(new Error('Network down'))
        .mockResolvedValueOnce(friendsPage([makeFriend('u2', 'user2')]))
      const wrapper = await mountFriendsList()

      await observer().trigger()
      await observer().trigger()
      expect(usernames(wrapper)).toEqual(['user1'])

      await observer().trigger()
      expect(friendsService.getUserFriends).toHaveBeenLastCalledWith({ next: 'cursor-2' })
      expect(usernames(wrapper)).toEqual(['user1', 'user2'])
    })
  })

  describe('links', () => {
    it('link to the direct conversation of each friend', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]),
      )
      const wrapper = await mountFriendsList()

      await observer().trigger()

      expect(link(wrapper, 'user1').attributes('href')).toBe('/chat/direct/dc-u1')
      expect(link(wrapper, 'user2').attributes('href')).toBe('/chat/direct/dc-u2')
    })

    it('highlight only the friend whose conversation is open', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]),
      )
      const wrapper = await mountFriendsList('/chat/direct/dc-u2')

      await observer().trigger()

      expect(link(wrapper, 'user1').classes()).not.toContain('background-light')
      expect(link(wrapper, 'user2').classes()).toContain('background-light')
    })

    it('highlight no friend when no conversation is open', async () => {
      friendsService.getUserFriends.mockResolvedValue(friendsPage([makeFriend('u1', 'user1')]))
      const wrapper = await mountFriendsList('/chat/direct')

      await observer().trigger()

      expect(link(wrapper, 'user1').classes()).not.toContain('background-light')
    })

    it('navigate to the conversation and select it in the store when clicked', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]),
      )
      const wrapper = await mountFriendsList('/chat/direct/dc-u1')
      await observer().trigger()

      await link(wrapper, 'user2').trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('directConversation')
      expect(router.currentRoute.value.params).toEqual({ directConversationId: 'dc-u2' })
      expect(directConversationStore().currentConversation).toEqual({
        directConversationId: 'dc-u2',
        chatConversationId: 'chat-u2',
        friend: { id: 'u2', username: 'user2' },
      })
      // Selected from the list: no request for the conversation
      expect(conversationService.getDirectConversationById).not.toHaveBeenCalled()
      expect(link(wrapper, 'user1').classes()).not.toContain('background-light')
      expect(link(wrapper, 'user2').classes()).toContain('background-light')
    })
  })

  describe('open conversation', () => {
    it('goes first while it is not in the pages loaded', async () => {
      directConversationStore().selectFriendConversation(makeFriend('u9', 'user9'))
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')], 'cursor-2'),
      )
      const wrapper = await mountFriendsList('/chat/direct/dc-u9')

      // Shown before any page is loaded
      expect(usernames(wrapper)).toEqual(['user9'])

      await observer().trigger()

      expect(usernames(wrapper)).toEqual(['user9', 'user1', 'user2'])
      expect(link(wrapper, 'user9').attributes('href')).toBe('/chat/direct/dc-u9')
      expect(link(wrapper, 'user9').classes()).toContain('background-light')
    })

    it('is shown in its place, only once, when its page is loaded', async () => {
      directConversationStore().selectFriendConversation(makeFriend('u3', 'user3'))
      friendsService.getUserFriends
        .mockResolvedValueOnce(
          friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')], 'cursor-2'),
        )
        .mockResolvedValueOnce(friendsPage([makeFriend('u3', 'user3'), makeFriend('u4', 'user4')]))
      const wrapper = await mountFriendsList('/chat/direct/dc-u3')

      await observer().trigger()
      expect(usernames(wrapper)).toEqual(['user3', 'user1', 'user2'])

      await observer().trigger()
      expect(usernames(wrapper)).toEqual(['user1', 'user2', 'user3', 'user4'])
    })

    it('is not repeated when it is already in the list', async () => {
      directConversationStore().selectFriendConversation(makeFriend('u2', 'user2'))
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]),
      )
      const wrapper = await mountFriendsList('/chat/direct/dc-u2')

      await observer().trigger()

      expect(usernames(wrapper)).toEqual(['user1', 'user2'])
    })

    it('is removed from the top when the conversation is closed', async () => {
      const store = directConversationStore()
      store.selectFriendConversation(makeFriend('u9', 'user9'))
      friendsService.getUserFriends.mockResolvedValue(friendsPage([makeFriend('u1', 'user1')]))
      const wrapper = await mountFriendsList('/chat/direct/dc-u9')
      await observer().trigger()

      store.clearDirectConversation()
      await flushPromises()

      expect(usernames(wrapper)).toEqual(['user1'])
    })
  })

  describe('presence', () => {
    beforeEach(() => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'user1'), makeFriend('u2', 'user2')]),
      )
    })

    it('shows every friend offline until their presence arrives', async () => {
      const wrapper = await mountFriendsList()
      await observer().trigger()

      expect(presenceOf(wrapper, 'user1')).toBe('offline')
      expect(presenceOf(wrapper, 'user2')).toBe('offline')
    })

    it('shows the friends of the snapshot online', async () => {
      receiveFriendsSnapshot(['u2'])

      const wrapper = await mountFriendsList()
      await observer().trigger()

      expect(presenceOf(wrapper, 'user1')).toBe('offline')
      expect(presenceOf(wrapper, 'user2')).toBe('online')
    })

    it('updates a friend when it goes online and offline', async () => {
      const wrapper = await mountFriendsList()
      await observer().trigger()

      receivePresence('u1', PresenceStatus.Online)
      await flushPromises()
      expect(presenceOf(wrapper, 'user1')).toBe('online')

      receivePresence('u1', PresenceStatus.Offline)
      await flushPromises()
      expect(presenceOf(wrapper, 'user1')).toBe('offline')
    })

    it('shows the presence of the open conversation that is not in the pages loaded', async () => {
      directConversationStore().selectFriendConversation(makeFriend('u9', 'user9'))
      receiveFriendsSnapshot(['u9'])

      const wrapper = await mountFriendsList('/chat/direct/dc-u9')

      expect(presenceOf(wrapper, 'user9')).toBe('online')
    })

    it('keeps the username as the accessible name of the link', async () => {
      receiveFriendsSnapshot(['u1'])
      const wrapper = await mountFriendsList()
      await observer().trigger()

      const indicator = link(wrapper, 'user1').find('[data-presence]')
      expect(indicator.attributes('aria-hidden')).toBe('true')
      expect(indicator.text()).toBe('')
    })
  })
})
