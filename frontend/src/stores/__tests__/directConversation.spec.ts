import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { directConversationStore } from '@/stores/directConversation'
import type { DirectConversation, UserFriendResponse } from '@/types'

const conversationService = vi.hoisted(() => ({ getDirectConversationById: vi.fn() }))
vi.mock('@/services/conversationService/ConversationService', () => ({
  default: conversationService,
}))

const me = { id: 'me', username: 'testuser' }
const friend = { id: 'u1', username: 'user1' }
const otherFriend = { id: 'u2', username: 'user2' }

function makeDirectConversation(id: string, user1 = me, user2 = friend): DirectConversation {
  return { id, chatConversationId: `chat-${id}`, user1, user2 }
}

function makeFriendResponse(id: string, user = friend): UserFriendResponse {
  return {
    friendRelationId: `relation-${id}`,
    friend: user,
    chatConversationId: `chat-${id}`,
    directConversationId: id,
  }
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

describe('directConversation store', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    setActivePinia(createPinia())
  })

  describe('updateDirectConversation', () => {
    it('requests the conversation and keeps the other user as the friend', async () => {
      conversationService.getDirectConversationById.mockResolvedValue(
        makeDirectConversation('dc1', me, friend)
      )
      const store = directConversationStore()

      const found = await store.updateDirectConversation('dc1', me.id)

      expect(found).toBe(true)
      expect(conversationService.getDirectConversationById).toHaveBeenCalledExactlyOnceWith('dc1')
      expect(store.currentConversation).toEqual({
        directConversationId: 'dc1',
        chatConversationId: 'chat-dc1',
        friend,
      })
    })

    it('keeps user1 as the friend when the current user is user2', async () => {
      conversationService.getDirectConversationById.mockResolvedValue(
        makeDirectConversation('dc1', friend, me)
      )
      const store = directConversationStore()

      await store.updateDirectConversation('dc1', me.id)

      expect(store.currentConversation?.friend).toEqual(friend)
    })

    it('does not request the conversation that is already the current one', async () => {
      const store = directConversationStore()
      store.selectFriendConversation(makeFriendResponse('dc1'))

      const found = await store.updateDirectConversation('dc1', me.id)

      expect(found).toBe(true)
      expect(conversationService.getDirectConversationById).not.toHaveBeenCalled()
    })

    it('returns false and clears the conversation when the API returns no content', async () => {
      conversationService.getDirectConversationById.mockResolvedValue(undefined)
      const store = directConversationStore()
      store.selectFriendConversation(makeFriendResponse('dc1'))

      const found = await store.updateDirectConversation('dc2', me.id)

      expect(found).toBe(false)
      expect(store.currentConversation).toBeUndefined()
    })

    it('throws the errors of the request and keeps the current conversation', async () => {
      conversationService.getDirectConversationById.mockRejectedValue(new Error('Network down'))
      const store = directConversationStore()
      store.selectFriendConversation(makeFriendResponse('dc1'))

      await expect(store.updateDirectConversation('dc2', me.id)).rejects.toThrow('Network down')
      expect(store.currentConversation?.directConversationId).toBe('dc1')
    })

    it('ignores a request that finishes after another conversation was requested', async () => {
      const first = deferred<DirectConversation>()
      conversationService.getDirectConversationById
        .mockReturnValueOnce(first.promise)
        .mockResolvedValueOnce(makeDirectConversation('dc2', me, otherFriend))
      const store = directConversationStore()

      const firstResult = store.updateDirectConversation('dc1', me.id)
      expect(await store.updateDirectConversation('dc2', me.id)).toBe(true)
      first.resolve(makeDirectConversation('dc1'))

      expect(await firstResult).toBe(false)
      expect(store.currentConversation?.directConversationId).toBe('dc2')
    })

    it('ignores a request that finishes after a friend was selected from the list', async () => {
      const request = deferred<DirectConversation>()
      conversationService.getDirectConversationById.mockReturnValue(request.promise)
      const store = directConversationStore()

      const result = store.updateDirectConversation('dc1', me.id)
      store.selectFriendConversation(makeFriendResponse('dc2', otherFriend))
      request.resolve(makeDirectConversation('dc1'))

      expect(await result).toBe(false)
      expect(store.currentConversation?.directConversationId).toBe('dc2')
    })

    it('ignores a request that finishes after the conversation was cleared', async () => {
      const request = deferred<DirectConversation>()
      conversationService.getDirectConversationById.mockReturnValue(request.promise)
      const store = directConversationStore()

      const result = store.updateDirectConversation('dc1', me.id)
      store.clearDirectConversation()
      request.resolve(makeDirectConversation('dc1'))

      expect(await result).toBe(false)
      expect(store.currentConversation).toBeUndefined()
    })
  })

  describe('selectFriendConversation', () => {
    it('keeps only the fields of the conversation', () => {
      const store = directConversationStore()

      store.selectFriendConversation(makeFriendResponse('dc1'))

      expect(store.currentConversation).toEqual({
        directConversationId: 'dc1',
        chatConversationId: 'chat-dc1',
        friend,
      })
    })
  })

  describe('clearDirectConversation', () => {
    it('removes the current conversation', () => {
      const store = directConversationStore()
      store.selectFriendConversation(makeFriendResponse('dc1'))

      store.clearDirectConversation()

      expect(store.currentConversation).toBeUndefined()
    })
  })
})
