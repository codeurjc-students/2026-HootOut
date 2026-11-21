import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import FriendsList from '@/components/directConversation/FriendsList.vue'
import type { FriendRelation, PageResult } from '@/types'

// ---------- mocks ----------

const friendsService = vi.hoisted(() => ({ getUserFriends: vi.fn() }))
vi.mock('@/services/friendsService/FriendsService', () => ({ default: friendsService }))

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

function makeFriend(id: string, username = id): FriendRelation {
  return {
    id: `relation-${id}`,
    friend: { id, username },
    chatConversationId: `conversation-${id}`,
    createdAt: '2026-11-21T15:00:00',
    modifiedAt: '2026-11-21T15:00:00',
  }
}

function friendsPage(
  items: FriendRelation[],
  next: string | null = null
): PageResult<FriendRelation> {
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

// Same paths as the app's router, with test components and no guards
function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/chat', name: 'chat', component: placeholder },
      {
        path: '/chat/direct/:chatConversationId?',
        name: 'directConversation',
        component: placeholder,
      },
    ],
  })
}

async function mountFriendsList(path = '/chat/direct') {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(FriendsList, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

const links = (wrapper: VueWrapper) => wrapper.findAll('a')
const usernames = (wrapper: VueWrapper) => links(wrapper).map((a) => a.text())
const link = (wrapper: VueWrapper, username: string) =>
  links(wrapper).find((a) => a.text() === username)!

// ---------- tests ----------

describe('FriendsList.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    FakeIntersectionObserver.instances = []
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver)
    router = createTestRouter()
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
        friendsPage([makeFriend('u1', 'alice'), makeFriend('u2', 'bob')])
      )
      const wrapper = await mountFriendsList()

      await observer().trigger()

      expect(usernames(wrapper)).toEqual(['alice', 'bob'])
    })

    it('adds the next page after the friends already loaded', async () => {
      friendsService.getUserFriends
        .mockResolvedValueOnce(
          friendsPage([makeFriend('u1', 'alice'), makeFriend('u2', 'bob')], 'cursor-2')
        )
        .mockResolvedValueOnce(friendsPage([makeFriend('u3', 'carol')]))
      const wrapper = await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenLastCalledWith({ next: 'cursor-2' })
      expect(usernames(wrapper)).toEqual(['alice', 'bob', 'carol'])
    })

    it('stops loading when there are no more pages', async () => {
      friendsService.getUserFriends.mockResolvedValue(friendsPage([makeFriend('u1', 'alice')]))
      await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenCalledTimes(1)
      expect(observer().disconnect).toHaveBeenCalled()
    })

    it('treats a page without next as the last page', async () => {
      const { next: _, ...lastPage } = friendsPage([makeFriend('u1', 'alice')])
      friendsService.getUserFriends.mockResolvedValue(lastPage)
      await mountFriendsList()

      await observer().trigger()
      await observer().trigger()

      expect(friendsService.getUserFriends).toHaveBeenCalledTimes(1)
    })

    it('does not request a page twice while one is loading', async () => {
      const request = deferred<PageResult<FriendRelation>>()
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
        .mockResolvedValueOnce(friendsPage([makeFriend('u1', 'alice')], 'cursor-2'))
        .mockRejectedValueOnce(new Error('Network down'))
        .mockResolvedValueOnce(friendsPage([makeFriend('u2', 'bob')]))
      const wrapper = await mountFriendsList()

      await observer().trigger()
      await observer().trigger()
      expect(usernames(wrapper)).toEqual(['alice'])

      await observer().trigger()
      expect(friendsService.getUserFriends).toHaveBeenLastCalledWith({ next: 'cursor-2' })
      expect(usernames(wrapper)).toEqual(['alice', 'bob'])
    })
  })

  describe('links', () => {
    it('link to the direct conversation with each friend', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'alice'), makeFriend('u2', 'bob')])
      )
      const wrapper = await mountFriendsList()

      await observer().trigger()

      expect(link(wrapper, 'alice').attributes('href')).toBe('/chat/direct/conversation-u1')
      expect(link(wrapper, 'bob').attributes('href')).toBe('/chat/direct/conversation-u2')
    })

    it('highlight only the friend whose conversation is open', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'alice'), makeFriend('u2', 'bob')])
      )
      const wrapper = await mountFriendsList('/chat/direct/conversation-u2')

      await observer().trigger()

      expect(link(wrapper, 'alice').classes()).not.toContain('background-light')
      expect(link(wrapper, 'bob').classes()).toContain('background-light')
    })

    it('highlight no friend when no conversation is open', async () => {
      friendsService.getUserFriends.mockResolvedValue(friendsPage([makeFriend('u1', 'alice')]))
      const wrapper = await mountFriendsList('/chat/direct')

      await observer().trigger()

      expect(link(wrapper, 'alice').classes()).not.toContain('background-light')
    })

    it('navigate to the conversation and move the highlight when clicked', async () => {
      friendsService.getUserFriends.mockResolvedValue(
        friendsPage([makeFriend('u1', 'alice'), makeFriend('u2', 'bob')])
      )
      const wrapper = await mountFriendsList('/chat/direct/conversation-u1')
      await observer().trigger()

      await link(wrapper, 'bob').trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('directConversation')
      expect(router.currentRoute.value.params).toEqual({ chatConversationId: 'conversation-u2' })
      expect(link(wrapper, 'alice').classes()).not.toContain('background-light')
      expect(link(wrapper, 'bob').classes()).toContain('background-light')
    })
  })
})
