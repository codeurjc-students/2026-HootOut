import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { conversationStore } from '@/stores/conversation'
import { WSMessageType, type ChatMessage, type PageResult, type WebSocketMessage } from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  messageService: { getMessagesByConversationId: vi.fn() },
  ws: { subscribe: vi.fn(), unsubscribe: vi.fn(), addPendingSubscription: vi.fn() },
}))

vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: mocks.messageService }))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))

const { messageService, ws } = mocks

// ---------- helpers ----------

function chatMessage(id: string): ChatMessage {
  return { id, content: `message ${id}` } as ChatMessage
}

function page(
  items: ChatMessage[],
  next: string | null = null,
  pageSize = 20
): PageResult<ChatMessage> {
  return { items, next, total: items.length, pageSize }
}

// A websocket message carrying a chat message, as the server sends it
function wsMessage(
  message: ChatMessage,
  options: { channel?: string; replyTo?: string } = {}
): WebSocketMessage {
  return {
    channel: options.channel,
    replyTo: options.replyTo,
    type: WSMessageType.ChatMessage,
    payload: JSON.stringify(message),
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((res) => {
    resolve = res
  })
  return { promise, resolve }
}

// Callback given to wsMessageService.subscribe() in the last subscription
function subscriptionCallback(): (message: WebSocketMessage) => void {
  const call = ws.subscribe.mock.lastCall
  if (!call) throw new Error('wsMessageService.subscribe() was not called')
  return call[1]
}

// Store with a conversation open and its first page of messages loaded
async function storeWithConversation(
  conversationId = 'conv-1',
  messages: ChatMessage[] = [chatMessage('m1')],
  next: string | null = 'cursor-2'
) {
  const store = conversationStore()
  messageService.getMessagesByConversationId.mockResolvedValueOnce(page(messages, next))
  await store.selectConversation(conversationId)
  messageService.getMessagesByConversationId.mockReset()
  ws.subscribe.mockClear()
  ws.unsubscribe.mockClear()
  return store
}

// ---------- tests ----------

describe('conversationStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  describe('initial state', () => {
    it('starts without a conversation', () => {
      const store = conversationStore()

      expect(store.currentConversationId).toBe('')
      expect(store.currentMessages).toEqual([])
      expect(store.pendingMessages).toEqual({})
      expect(store.lastPage).toBeUndefined()
    })
  })

  describe('selectConversation', () => {
    beforeEach(() => {
      messageService.getMessagesByConversationId.mockResolvedValue(
        page([chatMessage('m1')], 'cursor-2')
      )
    })

    it('opens the conversation and subscribes to it', async () => {
      const store = conversationStore()

      await store.selectConversation('conv-1')

      expect(store.currentConversationId).toBe('conv-1')
      expect(ws.subscribe).toHaveBeenCalledWith('conv-1', expect.any(Function))
    })

    it('loads the first page of messages of the conversation', async () => {
      const firstPage = page([chatMessage('m2'), chatMessage('m1')], 'cursor-2')
      messageService.getMessagesByConversationId.mockResolvedValueOnce(firstPage)
      const store = conversationStore()

      await store.selectConversation('conv-1')

      expect(messageService.getMessagesByConversationId).toHaveBeenCalledWith('conv-1')
      expect(store.currentMessages).toEqual(firstPage.items)
      expect(store.lastPage).toEqual(firstPage)
      expect(store.pendingMessages['conv-1']).toEqual({})
    })

    it('does not unsubscribe when no conversation was open', async () => {
      const store = conversationStore()

      await store.selectConversation('conv-1')

      expect(ws.unsubscribe).not.toHaveBeenCalled()
    })

    it('unsubscribes from the previous conversation', async () => {
      const store = await storeWithConversation('conv-1')
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([]))

      await store.selectConversation('conv-2')

      expect(ws.unsubscribe).toHaveBeenCalledWith('conv-1')
      expect(ws.subscribe).toHaveBeenCalledWith('conv-2', expect.any(Function))
    })

    it('clears the messages and pending messages of the previous conversation straight away', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)

      const selecting = store.selectConversation('conv-2')

      expect(store.currentMessages).toEqual([])
      expect(store.pendingMessages['conv-1']).toBeUndefined()

      request.resolve(page([chatMessage('c2')]))
      await selecting
    })

    it('adds a pending subscription when it cannot subscribe now', async () => {
      ws.subscribe.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })
      const store = conversationStore()

      await store.selectConversation('conv-1')

      expect(ws.addPendingSubscription).toHaveBeenCalledWith('conv-1', expect.any(Function))
      const callback = ws.addPendingSubscription.mock.lastCall![1]
      callback(wsMessage(chatMessage('new'), { channel: 'conv-1' }))
      expect(store.currentMessages[0]).toEqual(chatMessage('new'))
    })

    it('adds the messages received through the subscription', async () => {
      const store = conversationStore()
      await store.selectConversation('conv-1')

      subscriptionCallback()(wsMessage(chatMessage('new'), { channel: 'conv-1' }))

      expect(store.currentMessages[0]).toEqual(chatMessage('new'))
    })

    it('keeps the last selected conversation when an earlier one finishes loading later', async () => {
      const first = deferred<PageResult<ChatMessage>>()
      const second = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId
        .mockReturnValueOnce(first.promise)
        .mockReturnValueOnce(second.promise)
      const store = conversationStore()

      const selectingFirst = store.selectConversation('conv-1')
      const selectingSecond = store.selectConversation('conv-2')
      second.resolve(page([chatMessage('c2')]))
      await selectingSecond
      first.resolve(page([chatMessage('c1')]))
      await selectingFirst

      expect(store.currentConversationId).toBe('conv-2')
      expect(store.currentMessages).toEqual([chatMessage('c2')])
    })

    it('rejects when the messages cannot be loaded', async () => {
      messageService.getMessagesByConversationId.mockRejectedValueOnce(new Error('500'))
      const store = conversationStore()

      await expect(store.selectConversation('conv-1')).rejects.toThrow('500')
    })
  })

  describe('newMessage', () => {
    it('adds the chat message of the payload at the beginning', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])

      store.newMessage(wsMessage(chatMessage('m2'), { channel: 'conv-1' }))

      expect(store.currentMessages).toEqual([chatMessage('m2'), chatMessage('m1')])
    })

    it('ignores a message of another conversation', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])

      store.newMessage(wsMessage(chatMessage('other'), { channel: 'conv-2' }))

      expect(store.currentMessages).toEqual([chatMessage('m1')])
    })

    it('adds a message that does not say its conversation', async () => {
      const store = await storeWithConversation('conv-1', [])

      store.newMessage(wsMessage(chatMessage('m1')))

      expect(store.currentMessages).toEqual([chatMessage('m1')])
    })

    it.each([
      ['has no payload', undefined],
      ['has an empty payload', ''],
      ['has a payload that is not JSON', 'not json'],
    ])('ignores a message that %s', async (_, payload) => {
      const store = await storeWithConversation('conv-1', [])

      expect(() =>
        store.newMessage({ channel: 'conv-1', type: WSMessageType.ChatMessage, payload })
      ).not.toThrow()
      expect(store.currentMessages).toEqual([])
    })
  })

  describe('pending messages', () => {
    it('stores a pending message in the open conversation', async () => {
      const store = await storeWithConversation('conv-1')

      store.addPendingMessage('ack-1', chatMessage('p1'))

      expect(store.pendingMessages['conv-1']).toEqual({ 'ack-1': chatMessage('p1') })
    })

    it('does nothing when no conversation is open', () => {
      const store = conversationStore()

      store.addPendingMessage('ack-1', chatMessage('p1'))

      expect(store.pendingMessages).toEqual({})
    })

    it('confirms a pending message: removes it and adds the message from the ack', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.confirmMessage(wsMessage(chatMessage('saved-1'), { replyTo: 'ack-1' }))

      expect(store.pendingMessages['conv-1']).toEqual({})
      expect(store.currentMessages).toEqual([chatMessage('saved-1'), chatMessage('m1')])
    })

    it('confirms the message even when the ack carries another channel', async () => {
      const store = await storeWithConversation('conv-1', [])
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.confirmMessage(
        wsMessage(chatMessage('saved-1'), { replyTo: 'ack-1', channel: 'something-else' })
      )

      expect(store.currentMessages).toEqual([chatMessage('saved-1')])
    })

    it('only confirms the message the ack replies to', async () => {
      const store = await storeWithConversation('conv-1', [])
      store.addPendingMessage('ack-1', chatMessage('p1'))
      store.addPendingMessage('ack-2', chatMessage('p2'))

      store.confirmMessage(wsMessage(chatMessage('saved-1'), { replyTo: 'ack-1' }))

      expect(store.pendingMessages['conv-1']).toEqual({ 'ack-2': chatMessage('p2') })
    })

    it('ignores an ack for a message that is not pending', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])

      store.confirmMessage(wsMessage(chatMessage('other'), { replyTo: 'unknown' }))

      expect(store.currentMessages).toEqual([chatMessage('m1')])
    })

    it('ignores an ack without replyTo', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.confirmMessage(wsMessage(chatMessage('other')))

      expect(store.currentMessages).toEqual([chatMessage('m1')])
      expect(store.pendingMessages['conv-1']).toEqual({ 'ack-1': chatMessage('p1') })
    })

    it('ignores an ack that arrives after changing conversation', async () => {
      const store = await storeWithConversation('conv-1')
      store.addPendingMessage('ack-1', chatMessage('p1'))
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([chatMessage('c2')]))
      await store.selectConversation('conv-2')

      store.confirmMessage(wsMessage(chatMessage('saved-1'), { replyTo: 'ack-1' }))

      expect(store.currentMessages).toEqual([chatMessage('c2')])
    })

    it('ignores an ack when no conversation is open', () => {
      const store = conversationStore()

      expect(() =>
        store.confirmMessage(wsMessage(chatMessage('saved-1'), { replyTo: 'ack-1' }))
      ).not.toThrow()
      expect(store.currentMessages).toEqual([])
    })
    // Tests for removePendingMessage(), to paste at the end of describe('pending messages', ...)
    // in src/stores/__tests__/conversation.test.ts. They use the helpers that file already has:
    // storeWithConversation(), chatMessage(), page() and messageService.

    it('removes a pending message without adding it to the messages', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))
      store.addPendingMessage('ack-2', chatMessage('p2'))

      store.removePendingMessage('ack-1')

      expect(store.pendingMessages['conv-1']).toEqual({ 'ack-2': chatMessage('p2') })
      expect(store.currentMessages).toEqual([chatMessage('m1')])
    })

    it('ignores the removal of a message that is not pending', async () => {
      const store = await storeWithConversation('conv-1')
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.removePendingMessage('unknown')

      expect(store.pendingMessages['conv-1']).toEqual({ 'ack-1': chatMessage('p1') })
    })

    it('does not remove a pending message of another conversation', async () => {
      const store = await storeWithConversation('conv-1')
      store.addPendingMessage('ack-1', chatMessage('p1'))
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([]))
      await store.selectConversation('conv-2')
      store.pendingMessages['conv-1'] = { 'ack-1': chatMessage('p1') }

      store.removePendingMessage('ack-1')

      expect(store.pendingMessages['conv-1']).toEqual({ 'ack-1': chatMessage('p1') })
    })

    it('ignores the removal when no conversation is open', () => {
      const store = conversationStore()

      expect(() => store.removePendingMessage('ack-1')).not.toThrow()
      expect(store.pendingMessages).toEqual({})
    })
  })

  describe('getNextMessages', () => {
    it('requests the next page of the open conversation and appends it', async () => {
      const store = await storeWithConversation(
        'conv-1',
        [chatMessage('m3'), chatMessage('m2')],
        'cursor-2'
      )
      const nextPage = page([chatMessage('m1')], null)
      messageService.getMessagesByConversationId.mockResolvedValueOnce(nextPage)

      await store.getNextMessages()

      expect(messageService.getMessagesByConversationId).toHaveBeenCalledWith(
        'conv-1',
        'cursor-2',
        20
      )
      expect(store.currentMessages).toEqual([
        chatMessage('m3'),
        chatMessage('m2'),
        chatMessage('m1'),
      ])
      expect(store.lastPage).toEqual(nextPage)
    })

    it('uses the cursor of the last loaded page', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m3')], 'cursor-2')
      messageService.getMessagesByConversationId
        .mockResolvedValueOnce(page([chatMessage('m2')], 'cursor-3'))
        .mockResolvedValueOnce(page([chatMessage('m1')], null))

      await store.getNextMessages()
      await store.getNextMessages()

      expect(messageService.getMessagesByConversationId.mock.calls.map((call) => call[1])).toEqual([
        'cursor-2',
        'cursor-3',
      ])
      expect(store.currentMessages).toHaveLength(3)
    })

    it('keeps the messages when the next page is empty', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')], 'cursor-2')
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([], null))

      await store.getNextMessages()

      expect(store.currentMessages).toEqual([chatMessage('m1')])
      expect(store.lastPage?.next).toBeNull()
    })

    it('does not request anything when there are no more pages', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')], null)

      await store.getNextMessages()

      expect(messageService.getMessagesByConversationId).not.toHaveBeenCalled()
    })

    it('does not request anything when no conversation is open', async () => {
      const store = conversationStore()

      await store.getNextMessages()

      expect(messageService.getMessagesByConversationId).not.toHaveBeenCalled()
    })

    it('ignores the page when the conversation changed while loading it', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('c1-2')], 'cursor-2')
      const nextPage = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId
        .mockReturnValueOnce(nextPage.promise)
        .mockResolvedValueOnce(page([chatMessage('c2')]))

      const loadingMore = store.getNextMessages()
      await store.selectConversation('conv-2')
      nextPage.resolve(page([chatMessage('c1-1')]))
      await loadingMore

      expect(store.currentMessages).toEqual([chatMessage('c2')])
    })

    it('ignores the page when loading is cancelled', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m2')], 'cursor-2')
      const nextPage = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(nextPage.promise)

      const loadingMore = store.getNextMessages()
      store.cancelLoading()
      nextPage.resolve(page([chatMessage('m1')]))
      await loadingMore

      expect(store.currentMessages).toEqual([chatMessage('m2')])
    })
  })

  describe('cancelLoading', () => {
    it('makes a conversation that is still loading ignore its messages', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)
      const store = conversationStore()

      const selecting = store.selectConversation('conv-1')
      store.cancelLoading()
      request.resolve(page([chatMessage('m1')]))
      await selecting

      expect(store.currentMessages).toEqual([])
    })

    it('is not exposed as a number other stores could change', () => {
      const store = conversationStore()

      expect('selection' in store).toBe(false)
    })
  })

  describe('clearConversation', () => {
    it('clears the messages, the page and the pending messages but keeps the conversation open', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')], 'cursor-2')
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.clearConversation()

      expect(store.currentMessages).toEqual([])
      expect(store.lastPage).toEqual({ items: [], next: null, pageSize: 10, total: -1 })
      expect(store.pendingMessages['conv-1']).toBeUndefined()
      expect(store.currentConversationId).toBe('conv-1')
      expect(ws.unsubscribe).not.toHaveBeenCalled()
    })
  })

  describe('closeConversation', () => {
    it('unsubscribes, clears the messages and leaves no conversation open', async () => {
      const store = await storeWithConversation('conv-1', [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.closeConversation()

      expect(ws.unsubscribe).toHaveBeenCalledWith('conv-1')
      expect(store.currentConversationId).toBe('')
      expect(store.currentMessages).toEqual([])
      expect(store.pendingMessages['conv-1']).toBeUndefined()
    })

    it('ignores the messages that still arrive from the closed conversation', async () => {
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([]))
      const store = conversationStore()
      await store.selectConversation('conv-1')
      const callback = subscriptionCallback()

      store.closeConversation()
      callback(wsMessage(chatMessage('late'), { channel: 'conv-1' }))

      expect(store.currentMessages).toEqual([])
    })

    it('does not unsubscribe when no conversation is open', () => {
      const store = conversationStore()

      store.closeConversation()

      expect(ws.unsubscribe).not.toHaveBeenCalled()
    })

    it('does not fail when the socket is not connected', async () => {
      const store = await storeWithConversation('conv-1')
      ws.unsubscribe.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })

      expect(() => store.closeConversation()).not.toThrow()
      expect(store.currentConversationId).toBe('')
    })

    it('ignores the messages of a conversation that was still loading', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)
      const store = conversationStore()

      const selecting = store.selectConversation('conv-1')
      store.closeConversation()
      request.resolve(page([chatMessage('m1')]))
      await selecting

      expect(store.currentMessages).toEqual([])
    })

    it('does not subscribe twice when the same conversation is opened again', async () => {
      const store = await storeWithConversation('conv-1')
      store.closeConversation()
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([]))

      await store.selectConversation('conv-1')

      // closeConversation already unsubscribed: opening it again only subscribes
      expect(ws.unsubscribe).toHaveBeenCalledTimes(1)
      expect(ws.subscribe).toHaveBeenCalledTimes(1)
    })
  })
})
