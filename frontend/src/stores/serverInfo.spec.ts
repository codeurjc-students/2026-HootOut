import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { serverInfoStore } from '@/stores/serverInfo'
import { conversationStore } from '@/stores/conversation'
import type { ChatChannel, ChatMessage, ChatServer, PageResult } from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  serverService: { getServers: vi.fn(), getServerById: vi.fn() },
  channelService: { getChannelsByServerId: vi.fn() },
  messageService: { getMessagesByConversationId: vi.fn() },
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

// Each channel has its own conversation, with the id "conversation-<channel id>"
function channel(id: string): ChatChannel {
  return { id, name: id, chatConversationId: `conversation-${id}` }
}

const general = channel('general')
const random = channel('random')

function chatMessage(id: string): ChatMessage {
  return { id, content: `message ${id}` } as ChatMessage
}

function page<T>(items: T[], next: string | null = null): PageResult<T> {
  return { items, next, total: items.length, pageSize: 20 }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((res) => {
    resolve = res
  })
  return { promise, resolve }
}

// Store with the current server and its channels, optionally with one of them open
async function storeWithServer(
  channels: ChatChannel[] = [general, random],
  openChannelId?: string
) {
  const store = serverInfoStore()
  await store.setCurrentServerSettings({ id: 's1', name: 'My Server', channels })
  store.setServerChannels(channels)
  if (openChannelId) {
    messageService.getMessagesByConversationId.mockResolvedValueOnce(page([chatMessage('m1')]))
    await store.selectChannel(openChannelId)
  }
  vi.clearAllMocks()
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
      expect(store.servers).toEqual([])
    })
  })

  describe('channels', () => {
    it('adds a new channel and returns true', () => {
      const store = serverInfoStore()

      expect(store.addNewChannel(general)).toBe(true)
      expect(store.channels).toEqual([general])
    })

    it('does not add the same channel twice', () => {
      const store = serverInfoStore()
      store.addNewChannel(general)

      expect(store.addNewChannel(general)).toBe(false)
      expect(store.channels).toHaveLength(1)
    })

    it('does not touch the pending messages of the conversation when adding a channel', () => {
      const store = serverInfoStore()

      store.addNewChannel(general)

      expect(conversationStore().pendingMessages).toEqual({})
    })

    it('adds all the channels of a server', () => {
      const store = serverInfoStore()

      store.setServerChannels([general, random])

      expect(store.channels).toEqual([general, random])
    })

    it('sets the current channel', () => {
      const store = serverInfoStore()

      store.setCurrentChatChannel(general)
      expect(store.currentChatChannel).toEqual(general)

      store.setCurrentChatChannel(undefined)
      expect(store.currentChatChannel).toBeUndefined()
    })
  })

  describe('selectChannel', () => {
    it('does nothing for a channel that is not in the list', async () => {
      const store = await storeWithServer([general])

      await store.selectChannel('unknown')

      expect(store.currentChatChannel).toBeUndefined()
      expect(ws.subscribe).not.toHaveBeenCalled()
      expect(messageService.getMessagesByConversationId).not.toHaveBeenCalled()
    })

    it('opens the channel and selects its conversation', async () => {
      const store = await storeWithServer()
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([chatMessage('m1')]))

      await store.selectChannel('random')

      expect(store.currentChatChannel).toEqual(random)
      expect(conversationStore().currentConversationId).toBe('conversation-random')
      expect(ws.subscribe).toHaveBeenCalledWith('conversation-random', expect.any(Function))
      expect(messageService.getMessagesByConversationId).toHaveBeenCalledWith('conversation-random')
    })

    it('marks the channel as open straight away, before its messages are loaded', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)
      const store = await storeWithServer()

      const selecting = store.selectChannel('general')
      expect(store.currentChatChannel).toEqual(general)

      request.resolve(page([chatMessage('m1')]))
      await selecting
    })

    it('finishes once the messages of the conversation are loaded', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)
      const store = await storeWithServer()
      const finished = vi.fn()

      const selecting = store.selectChannel('general').then(finished)
      // Lets every pending promise settle: only the messages request is still waiting
      await new Promise((resolve) => setTimeout(resolve, 0))
      expect(finished).not.toHaveBeenCalled()

      request.resolve(page([chatMessage('m1')]))
      await selecting
      expect(conversationStore().currentMessages).toEqual([chatMessage('m1')])
    })

    it('rejects when the messages cannot be loaded', async () => {
      messageService.getMessagesByConversationId.mockRejectedValueOnce(new Error('500'))
      const store = await storeWithServer()

      await expect(store.selectChannel('general')).rejects.toThrow('500')
    })

    it('keeps the last selected channel and its messages', async () => {
      const generalRequest = deferred<PageResult<ChatMessage>>()
      const randomRequest = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId
        .mockReturnValueOnce(generalRequest.promise)
        .mockReturnValueOnce(randomRequest.promise)
      const store = await storeWithServer()

      const selectingGeneral = store.selectChannel('general')
      const selectingRandom = store.selectChannel('random')
      randomRequest.resolve(page([chatMessage('r1')]))
      await selectingRandom
      generalRequest.resolve(page([chatMessage('g1')]))
      await selectingGeneral

      expect(store.currentChatChannel).toEqual(random)
      expect(conversationStore().currentMessages).toEqual([chatMessage('r1')])
    })
  })

  describe('servers', () => {
    it('requests a page of servers, appends them and returns the page', async () => {
      const serversPage = page([{ id: 's1' }, { id: 's2' }] as ChatServer[], 'cursor-2')
      serverService.getServers.mockResolvedValueOnce(serversPage)
      const store = serverInfoStore()

      expect(await store.getChatServers(null)).toEqual(serversPage)
      expect(serverService.getServers).toHaveBeenCalledWith(null)
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

    it('replaces the channels and leaves the conversation of the previous server', async () => {
      const store = await storeWithServer([general, random], 'general')
      const other = channel('other')
      serverService.getServerById.mockResolvedValueOnce({ id: 's2', channels: [other] })

      await store.setCurrentServer('s2')

      expect(store.channels).toEqual([other])
      expect(store.currentChatChannel).toBeUndefined()
      expect(conversationStore().currentConversationId).toBe('')
      expect(conversationStore().currentMessages).toEqual([])
      expect(ws.unsubscribe).toHaveBeenCalledWith('conversation-general')
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
      const store = await storeWithServer([general])

      await store.fetchServerChannels(store.currentServer!)

      expect(channelService.getChannelsByServerId).not.toHaveBeenCalled()
      expect(store.channels).toEqual([general])
    })

    it('requests the channels of the server when makeRequest is true', async () => {
      channelService.getChannelsByServerId.mockResolvedValueOnce([general, random])
      const store = await storeWithServer([])

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

    it('closes the open channel and leaves its conversation', async () => {
      const store = await storeWithServer([general, random], 'general')

      await store.fetchServerChannels(store.currentServer!)

      expect(store.currentChatChannel).toBeUndefined()
      expect(conversationStore().currentConversationId).toBe('')
      expect(conversationStore().currentMessages).toEqual([])
      expect(ws.unsubscribe).toHaveBeenCalledWith('conversation-general')
    })

    it('does not open a conversation that finishes loading after changing server', async () => {
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)
      const store = await storeWithServer([general])

      const selecting = store.selectChannel('general')
      await store.fetchServerChannels(store.currentServer!)
      request.resolve(page([chatMessage('g1')]))
      await selecting

      expect(store.currentChatChannel).toBeUndefined()
      expect(conversationStore().currentMessages).toEqual([])
    })

    it('ignores a conversation that finishes loading while the channels are being requested', async () => {
      const messages = deferred<PageResult<ChatMessage>>()
      const channels = deferred<ChatChannel[]>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(messages.promise)
      channelService.getChannelsByServerId.mockReturnValueOnce(channels.promise)
      const store = await storeWithServer([general])

      const selecting = store.selectChannel('general')
      const fetching = store.fetchServerChannels(store.currentServer!, true)
      messages.resolve(page([chatMessage('g1')])) // arrives before the channels
      await selecting

      expect(conversationStore().currentMessages).toEqual([])

      channels.resolve([general])
      await fetching
    })
  })
})
