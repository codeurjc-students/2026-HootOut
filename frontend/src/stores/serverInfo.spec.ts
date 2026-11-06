import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { serverInfoStore } from '@/stores/serverInfo'
import {
  WSMessageType,
  type ChatChannel,
  type ChatMessage,
  type ChatServer,
  type PageResult,
  type WebSocketMessage,
} from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  serverService: { getServers: vi.fn(), getServerById: vi.fn() },
  channelService: { getChannelsByServerId: vi.fn() },
  messageService: { getMessagesByChannelId: vi.fn() },
  ws: { subscribe: vi.fn(), unsubscribe: vi.fn(), addPendingSubscription: vi.fn() },
}))

vi.mock('@/services/chatServer/ChatServerService', () => ({ default: mocks.serverService }))
vi.mock('@/services/chatChannel/ChatChannelService', () => ({ default: mocks.channelService }))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: mocks.messageService }))
vi.mock('@/services/common/websocketsMesage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))

const { serverService, channelService, messageService, ws } = mocks

// ---------- helpers ----------

const general: ChatChannel = {
  id: 'general',
  name: 'General',
  createdAt: '',
  modifiedAt: '',
  serverId: '1',
}
const random: ChatChannel = {
  id: 'random',
  name: 'Random',
  createdAt: '',
  modifiedAt: '',
  serverId: '1',
}

function chatMessage(id: string, content = `message ${id}`): ChatMessage {
  return { id, content } as ChatMessage
}

function page<T>(items: T[], next: string | null = null, pageSize = 20): PageResult<T> {
  return { items, next, total: items.length, pageSize }
}

// A websocket message carrying a chat message, as the server sends it
function wsChatMessage(message: ChatMessage, replyTo?: string): WebSocketMessage {
  return {
    channel: 'general',
    type: WSMessageType.ChatMessage,
    payload: JSON.stringify(message),
    replyTo,
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

// Store with the given channels and one of them open, with its first page of messages
async function storeWithOpenChannel(
  channel = general,
  messages: ChatMessage[] = [chatMessage('m1')],
  next: string | null = 'cursor-2'
) {
  const store = serverInfoStore()
  store.setServerChannels([general, random])
  messageService.getMessagesByChannelId.mockResolvedValueOnce(page(messages, next))
  await store.selectChannel(channel.id)
  messageService.getMessagesByChannelId.mockReset()
  ws.subscribe.mockClear()
  return store
}

// ---------- tests ----------

describe('serverInfoStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  describe('initial state', () => {
    it('starts empty', () => {
      const store = serverInfoStore()

      expect(store.currentChatChannel).toBeUndefined()
      expect(store.currentServer).toBeUndefined()
      expect(store.channels).toEqual([])
      expect(store.channelMessages).toEqual([])
      expect(store.servers).toEqual([])
      expect(store.pendingMessages).toEqual({})
      expect(store.lastPage).toBeUndefined()
    })
  })

  describe('channels', () => {
    it('adds a new channel with no pending messages and returns true', () => {
      const store = serverInfoStore()

      expect(store.addNewChannel(general)).toBe(true)
      expect(store.channels).toEqual([general])
      expect(store.pendingMessages).toEqual({ general: {} })
    })

    it('does not add the same channel twice', () => {
      const store = serverInfoStore()
      store.addNewChannel(general)

      expect(store.addNewChannel(general)).toBe(false)
      expect(store.channels).toHaveLength(1)
    })

    it('adds all the channels of a server', () => {
      const store = serverInfoStore()

      store.setServerChannels([general, random])

      expect(store.channels).toEqual([general, random])
      expect(store.pendingMessages).toEqual({ general: {}, random: {} })
    })

    it('sets the current channel', () => {
      const store = serverInfoStore()

      store.setCurrentChatChannel(general)
      expect(store.currentChatChannel).toEqual(general)

      store.setCurrentChatChannel(undefined)
      expect(store.currentChatChannel).toBeUndefined()
    })

    it('replaces the messages of a channel and resets its pending messages', () => {
      const store = serverInfoStore()
      store.addNewChannel(general)
      store.setCurrentChatChannel(general)
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.setChannelMessages(general, [chatMessage('m1')])

      expect(store.channelMessages).toEqual([chatMessage('m1')])
      expect(store.pendingMessages.general).toEqual({})
    })
  })

  describe('selectChannel', () => {
    beforeEach(() => {
      messageService.getMessagesByChannelId.mockResolvedValue(page([chatMessage('m1')], 'cursor-2'))
    })

    it('does nothing for a channel that is not in the list', async () => {
      const store = serverInfoStore()
      store.setServerChannels([general])

      await store.selectChannel('unknown')

      expect(ws.subscribe).not.toHaveBeenCalled()
      expect(messageService.getMessagesByChannelId).not.toHaveBeenCalled()
      expect(store.currentChatChannel).toBeUndefined()
    })

    it('subscribes to the websocket channel with the channel id', async () => {
      const store = serverInfoStore()
      store.setServerChannels([general])

      await store.selectChannel('general')

      expect(ws.subscribe).toHaveBeenCalledWith('general', expect.any(Function))
    })

    it('adds the messages received through the subscription', async () => {
      const store = serverInfoStore()
      store.setServerChannels([general])
      await store.selectChannel('general')

      subscriptionCallback()(wsChatMessage(chatMessage('new')))

      expect(store.channelMessages[0]).toEqual(chatMessage('new'))
    })

    it('adds a pending subscription when it cannot subscribe now', async () => {
      ws.subscribe.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })
      const store = serverInfoStore()
      store.setServerChannels([general])

      await store.selectChannel('general')

      expect(ws.addPendingSubscription).toHaveBeenCalledWith('general', expect.any(Function))
      const callback = ws.addPendingSubscription.mock.lastCall![1]
      callback(wsChatMessage(chatMessage('new')))
      expect(store.channelMessages[0]).toEqual(chatMessage('new'))
    })

    it('loads the messages of the channel and opens it', async () => {
      const firstPage = page([chatMessage('m2'), chatMessage('m1')], 'cursor-2')
      messageService.getMessagesByChannelId.mockResolvedValueOnce(firstPage)
      const store = serverInfoStore()
      store.setServerChannels([general])

      await store.selectChannel('general')

      expect(messageService.getMessagesByChannelId).toHaveBeenCalledWith('general')
      expect(store.channelMessages).toEqual(firstPage.items)
      expect(store.lastPage).toEqual(firstPage)
      expect(store.currentChatChannel).toEqual(general)
    })

    it('opens the channel only after its messages are loaded', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByChannelId.mockReturnValueOnce(request.promise)
      const store = serverInfoStore()
      store.setServerChannels([general])

      const selecting = store.selectChannel('general')
      expect(store.currentChatChannel).toBeUndefined()

      request.resolve(page([chatMessage('m1')]))
      await selecting
      expect(store.currentChatChannel).toEqual(general)
    })

    it('replaces the messages of the previous channel', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('g1')])
      messageService.getMessagesByChannelId.mockResolvedValueOnce(page([chatMessage('r1')]))

      await store.selectChannel('random')

      expect(store.channelMessages).toEqual([chatMessage('r1')])
      expect(store.currentChatChannel).toEqual(random)
    })

    it('keeps the last selected channel when an earlier one finishes loading later', async () => {
      const generalRequest = deferred<PageResult<ChatMessage>>()
      const randomRequest = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByChannelId
        .mockReturnValueOnce(generalRequest.promise)
        .mockReturnValueOnce(randomRequest.promise)
      const store = serverInfoStore()
      store.setServerChannels([general, random])

      const selectingGeneral = store.selectChannel('general')
      const selectingRandom = store.selectChannel('random')
      randomRequest.resolve(page([chatMessage('r1')]))
      await selectingRandom
      generalRequest.resolve(page([chatMessage('g1')]))
      await selectingGeneral

      expect(store.currentChatChannel).toEqual(random)
      expect(store.channelMessages).toEqual([chatMessage('r1')])
    })

    it('does not open a channel of the previous server that finishes loading after changing server', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByChannelId.mockReturnValueOnce(request.promise)
      const store = serverInfoStore()
      store.setServerChannels([general])

      const selecting = store.selectChannel('general')
      store.setCurrentServerSettings({ id: 'server-2', channels: [random] } as ChatServer)
      await store.fetchServerChannels(store.currentServer!)
      request.resolve(page([chatMessage('g1')]))
      await selecting

      expect(store.currentChatChannel).toBeUndefined()
      expect(store.channelMessages).toEqual([])
    })
  })

  describe('newMessage', () => {
    it('adds the chat message of the payload at the beginning', () => {
      const store = serverInfoStore()
      store.newMessage(wsChatMessage(chatMessage('m1')))

      store.newMessage(wsChatMessage(chatMessage('m2')))

      expect(store.channelMessages).toEqual([chatMessage('m2'), chatMessage('m1')])
    })

    it.each([
      ['has no payload', undefined],
      ['has an empty payload', ''],
      ['has a payload that is not JSON', 'not json'],
    ])('ignores a message that %s', (_, payload) => {
      const store = serverInfoStore()

      expect(() =>
        store.newMessage({ channel: 'general', type: WSMessageType.ChatMessage, payload })
      ).not.toThrow()
      expect(store.channelMessages).toEqual([])
    })
  })

  describe('pending messages', () => {
    it('stores a pending message in the open channel', async () => {
      const store = await storeWithOpenChannel(general)

      store.addPendingMessage('ack-1', chatMessage('p1'))

      expect(store.pendingMessages.general).toEqual({ 'ack-1': chatMessage('p1') })
    })

    it('does nothing when no channel is open', () => {
      const store = serverInfoStore()

      expect(() => store.addPendingMessage('ack-1', chatMessage('p1'))).not.toThrow()
      expect(store.pendingMessages).toEqual({})
    })

    it('confirms a pending message: removes it and adds the message from the ack', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.confirmMessage(wsChatMessage(chatMessage('saved-1'), 'ack-1'))

      expect(store.pendingMessages.general).toEqual({})
      expect(store.channelMessages).toEqual([chatMessage('saved-1'), chatMessage('m1')])
    })

    it('only confirms the message the ack replies to', async () => {
      const store = await storeWithOpenChannel(general, [])
      store.addPendingMessage('ack-1', chatMessage('p1'))
      store.addPendingMessage('ack-2', chatMessage('p2'))

      store.confirmMessage(wsChatMessage(chatMessage('saved-1'), 'ack-1'))

      expect(store.pendingMessages.general).toEqual({ 'ack-2': chatMessage('p2') })
    })

    it('ignores an ack for a message that is not pending', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('m1')])

      store.confirmMessage(wsChatMessage(chatMessage('other'), 'unknown-ack'))

      expect(store.channelMessages).toEqual([chatMessage('m1')])
    })

    it('ignores an ack without replyTo', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('m1')])
      store.addPendingMessage('ack-1', chatMessage('p1'))

      store.confirmMessage(wsChatMessage(chatMessage('other')))

      expect(store.channelMessages).toEqual([chatMessage('m1')])
      expect(store.pendingMessages.general).toEqual({ 'ack-1': chatMessage('p1') })
    })

    it('ignores an ack that arrives after changing channel', async () => {
      const store = await storeWithOpenChannel(general)
      store.addPendingMessage('ack-1', chatMessage('p1'))
      messageService.getMessagesByChannelId.mockResolvedValueOnce(page([chatMessage('r1')]))
      await store.selectChannel('random')

      expect(() =>
        store.confirmMessage(wsChatMessage(chatMessage('saved-1'), 'ack-1'))
      ).not.toThrow()
      expect(store.channelMessages).toEqual([chatMessage('r1')])
    })

    it('ignores an ack when no channel is open', () => {
      const store = serverInfoStore()

      expect(() =>
        store.confirmMessage(wsChatMessage(chatMessage('saved-1'), 'ack-1'))
      ).not.toThrow()
      expect(store.channelMessages).toEqual([])
    })
  })

  describe('getNextMessages', () => {
    it('requests the next page of the open channel and appends it', async () => {
      const store = await storeWithOpenChannel(
        general,
        [chatMessage('m3'), chatMessage('m2')],
        'cursor-2'
      )
      const nextPage = page([chatMessage('m1')], null)
      messageService.getMessagesByChannelId.mockResolvedValueOnce(nextPage)

      await store.getNextMessages()

      expect(messageService.getMessagesByChannelId).toHaveBeenCalledWith('general', 'cursor-2', 20)
      expect(store.channelMessages).toEqual([
        chatMessage('m3'),
        chatMessage('m2'),
        chatMessage('m1'),
      ])
      expect(store.lastPage).toEqual(nextPage)
    })

    it('uses the cursor of the last loaded page', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('m3')], 'cursor-2')
      messageService.getMessagesByChannelId
        .mockResolvedValueOnce(page([chatMessage('m2')], 'cursor-3'))
        .mockResolvedValueOnce(page([chatMessage('m1')], null))

      await store.getNextMessages()
      await store.getNextMessages()

      expect(messageService.getMessagesByChannelId.mock.calls.map((call) => call[1])).toEqual([
        'cursor-2',
        'cursor-3',
      ])
      expect(store.channelMessages).toHaveLength(3)
    })

    it('keeps the messages when the next page is empty', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('m1')], 'cursor-2')
      messageService.getMessagesByChannelId.mockResolvedValueOnce(page([], null))

      await store.getNextMessages()

      expect(store.channelMessages).toEqual([chatMessage('m1')])
      expect(store.lastPage?.next).toBeNull()
    })

    it('does not request anything when there are no more pages', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('m1')], null)

      await store.getNextMessages()

      expect(messageService.getMessagesByChannelId).not.toHaveBeenCalled()
      expect(store.channelMessages).toEqual([chatMessage('m1')])
    })

    it('does not request anything when no channel has been loaded', async () => {
      const store = serverInfoStore()

      await store.getNextMessages()

      expect(messageService.getMessagesByChannelId).not.toHaveBeenCalled()
    })

    it('ignores the page when the channel changed while loading it', async () => {
      const store = await storeWithOpenChannel(general, [chatMessage('g2')], 'cursor-2')
      const nextPage = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByChannelId
        .mockReturnValueOnce(nextPage.promise)
        .mockResolvedValueOnce(page([chatMessage('r1')]))

      const loadingMore = store.getNextMessages()
      await store.selectChannel('random')
      nextPage.resolve(page([chatMessage('g1')]))
      await loadingMore

      expect(store.channelMessages).toEqual([chatMessage('r1')])
    })
  })

  describe('servers', () => {
    it('requests a page of servers, appends them and returns the page', async () => {
      const serversPage = page([{ id: 's1' }, { id: 's2' }] as ChatServer[], 'cursor-2')
      serverService.getServers.mockResolvedValueOnce(serversPage)
      const store = serverInfoStore()

      const result = await store.getChatServers(null)

      expect(serverService.getServers).toHaveBeenCalledWith(null)
      expect(result).toEqual(serversPage)
      expect(store.servers).toEqual(serversPage.items)
    })

    it('appends the next page of servers', async () => {
      serverService.getServers
        .mockResolvedValueOnce(page([{ id: 's1' }] as ChatServer[], 'cursor-2'))
        .mockResolvedValueOnce(page([{ id: 's2' }] as ChatServer[]))
      const store = serverInfoStore()

      await store.getChatServers(null)
      await store.getChatServers('cursor-2')

      expect(serverService.getServers).toHaveBeenLastCalledWith('cursor-2')
      expect(store.servers.map((s) => s.id)).toEqual(['s1', 's2'])
    })

    it('returns the page without changing the servers when it has no items', async () => {
      const emptyPage = { next: null, total: 0, pageSize: 20 } as unknown as PageResult<ChatServer>
      serverService.getServers.mockResolvedValueOnce(emptyPage)
      const store = serverInfoStore()

      expect(await store.getChatServers(null)).toBe(emptyPage)
      expect(store.servers).toEqual([])
    })

    it('adds a server', () => {
      const store = serverInfoStore()

      store.addChatServer({ id: 's1' } as ChatServer)

      expect(store.servers).toEqual([{ id: 's1' }])
    })
  })

  describe('setCurrentServer', () => {
    it('loads the server and shows its channels with none open', async () => {
      serverService.getServerById.mockResolvedValueOnce({ id: 's1', channels: [general, random] })
      const store = serverInfoStore()

      await store.setCurrentServer('s1')

      expect(serverService.getServerById).toHaveBeenCalledWith('s1')
      expect(store.currentServer?.id).toBe('s1')
      expect(store.channels).toEqual([general, random])
      expect(store.currentChatChannel).toBeUndefined()
    })

    it('replaces the channels and closes the channel of the previous server', async () => {
      const store = await storeWithOpenChannel(general)
      serverService.getServerById.mockResolvedValueOnce({ id: 's2', channels: [{ id: 'other' }] })

      await store.setCurrentServer('s2')

      expect(store.channels).toEqual([{ id: 'other' }])
      expect(store.currentChatChannel).toBeUndefined()
      expect(store.channelMessages).toEqual([])
    })

    it('does not touch the channels when the server is not found', async () => {
      serverService.getServerById.mockResolvedValueOnce(undefined)
      const store = serverInfoStore()
      store.setServerChannels([general])

      await store.setCurrentServer('missing')

      expect(store.currentServer).toBeUndefined()
      expect(store.channels).toEqual([general])
    })

    it('sets the server of the settings page without any request', async () => {
      const store = serverInfoStore()

      await store.setCurrentServerSettings({ id: 's1' } as ChatServer)

      expect(store.currentServer).toEqual({ id: 's1' })
      expect(serverService.getServerById).not.toHaveBeenCalled()
    })
  })

  describe('fetchServerChannels', () => {
    it('uses the channels of the current server without a request by default', async () => {
      const store = serverInfoStore()
      await store.setCurrentServerSettings({ id: 's1', channels: [general] } as ChatServer)

      await store.fetchServerChannels(store.currentServer!)

      expect(channelService.getChannelsByServerId).not.toHaveBeenCalled()
      expect(store.channels).toEqual([general])
    })

    it('requests the channels of the server when makeRequest is true', async () => {
      channelService.getChannelsByServerId.mockResolvedValueOnce([general, random])
      const store = serverInfoStore()
      await store.setCurrentServerSettings({
        id: 's1',
        channels: [],
        name: 's1',
        modifiedAt: '',
        createdAt: '',
        description: '',
      } as ChatServer)

      await store.fetchServerChannels(store.currentServer!, true)

      expect(channelService.getChannelsByServerId).toHaveBeenCalledWith('s1')
      expect(store.currentServer?.channels).toEqual([general, random])
      expect(store.channels).toEqual([general, random])
    })

    it('shows no channels when the server has none', async () => {
      const store = serverInfoStore()
      store.setServerChannels([general])
      await store.setCurrentServerSettings({ id: 's1' } as ChatServer)

      await store.fetchServerChannels(store.currentServer!)

      expect(store.channels).toEqual([])
    })

    it('closes the open channel and clears its messages and pending messages', async () => {
      const store = await storeWithOpenChannel(general)
      store.addPendingMessage('ack-1', chatMessage('p1'))
      await store.setCurrentServerSettings({ id: 's1', channels: [random] } as ChatServer)

      await store.fetchServerChannels(store.currentServer!)

      expect(store.currentChatChannel).toBeUndefined()
      expect(store.channelMessages).toEqual([])
      expect(store.pendingMessages.general).toBeUndefined()
    })
  })
})
