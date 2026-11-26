import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { WebSocketMessageService } from '@/services/common/websocketsMessage/WebSocketsMessageService'
import type { WebSocketHandlers } from '@/services/common/websockets/WebSocketService'
import { WSMessageType, type WebSocketMessage } from '@/types'

// ---------- mocks ----------

const socket = vi.hoisted(() => ({
  connect: vi.fn(),
  send: vi.fn(),
  close: vi.fn(),
  isConnected: vi.fn(),
}))
const api = vi.hoisted(() => ({ post: vi.fn() }))

vi.mock('@/services/common/websockets/WebSocketService', () => ({ default: socket }))
vi.mock('@/services/common/api/ApiService', () => ({ post: api.post }))
vi.mock('@/config', () => ({ getConfig: () => ({ WSS_URL: 'wss://test' }) }))

// ---------- helpers ----------

// Handlers passed to webSocketService.connect() in the last connection
function handlers(): Required<WebSocketHandlers> {
  const call = socket.connect.mock.lastCall
  if (!call) throw new Error('webSocketService.connect() was not called')
  return call[1]
}

async function connectService(service: WebSocketMessageService) {
  await service.connect(vi.fn(async () => {}))
  await handlers().onOpen(new Event('open'))
}

function receive(message: WebSocketMessage) {
  handlers().onMessage(new MessageEvent('message', { data: JSON.stringify(message) }))
}

const sentMessages = (): WebSocketMessage[] =>
  socket.send.mock.calls.map(([data]) => JSON.parse(data))
const lastSentMessage = () => sentMessages().at(-1)!

const presenceMessage: WebSocketMessage = {
  type: WSMessageType.Presence,
  channel: 'presence.user.u1',
  payload: JSON.stringify({ UserId: 'u1', Status: 10 }),
}

// ---------- tests ----------

describe('WebSocketMessageService presence', () => {
  let service: WebSocketMessageService

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] })
    vi.resetAllMocks()
    socket.isConnected.mockReturnValue(false)
    api.post.mockResolvedValue({ ticket: 'ticket-1' })
    service = new WebSocketMessageService()
    await connectService(service)
  })

  afterEach(() => {
    // The acks that a test leaves pending are dropped with the fake timers
    vi.useRealTimers()
  })

  describe('type handlers', () => {
    it('receives every message of its type, whatever its channel', () => {
      const handler = vi.fn()
      service.setTypeHandler(WSMessageType.Presence, handler)

      receive(presenceMessage)
      receive({ ...presenceMessage, channel: 'presence.server.s1' })

      expect(handler).toHaveBeenCalledTimes(2)
      expect(handler).toHaveBeenNthCalledWith(1, presenceMessage)
    })

    it('takes the message before the subscription of its channel', () => {
      const handler = vi.fn()
      const channelCallback = vi.fn()
      service.subscribe('s1', channelCallback)
      service.setTypeHandler(WSMessageType.PresenceSnapshot, handler)

      receive({
        type: WSMessageType.PresenceSnapshot,
        channel: 's1',
        payload: '{"OnlineUserIds":[]}',
      })

      expect(handler).toHaveBeenCalledOnce()
      expect(channelCallback).not.toHaveBeenCalled()
    })

    it('does not receive messages of other types', () => {
      const handler = vi.fn()
      const channelCallback = vi.fn()
      service.setTypeHandler(WSMessageType.Presence, handler)
      service.subscribe('conversation-1', channelCallback)

      receive({ type: WSMessageType.ChatMessage, channel: 'conversation-1', payload: '{}' })

      expect(handler).not.toHaveBeenCalled()
      expect(channelCallback).toHaveBeenCalledOnce()
    })

    it('is replaced by a later handler of the same type', () => {
      const first = vi.fn()
      const second = vi.fn()
      service.setTypeHandler(WSMessageType.Presence, first)
      service.setTypeHandler(WSMessageType.Presence, second)

      receive(presenceMessage)

      expect(first).not.toHaveBeenCalled()
      expect(second).toHaveBeenCalledOnce()
    })

    it('stops receiving after being removed', () => {
      const handler = vi.fn()
      service.setTypeHandler(WSMessageType.Presence, handler)

      service.removeTypeHandler(WSMessageType.Presence)
      receive(presenceMessage)

      expect(handler).not.toHaveBeenCalled()
    })

    it('does not receive the acks of its own messages', async () => {
      const handler = vi.fn()
      service.setTypeHandler(WSMessageType.Ack, handler)
      const ack = service.presenceSubscribe('s1')

      receive({ type: WSMessageType.Ack, channel: 's1', replyTo: lastSentMessage().id })
      await ack

      expect(handler).not.toHaveBeenCalled()
    })
  })

  describe('presenceSubscribe', () => {
    it('sends a PresenceSubscribe with the server as channel and an id for the ack', () => {
      void service.presenceSubscribe('s1')

      expect(lastSentMessage()).toEqual({
        type: WSMessageType.PresenceSubscribe,
        channel: 's1',
        id: expect.any(String),
      })
    })

    it('resolves with the ack', async () => {
      const result = service.presenceSubscribe('s1')
      const ack = { type: WSMessageType.Ack, channel: 's1', replyTo: lastSentMessage().id }

      receive(ack)

      await expect(result).resolves.toEqual(ack)
    })

    it('rejects when the server answers with an error (e.g. not a member)', async () => {
      const result = service.presenceSubscribe('s1')

      receive({
        type: WSMessageType.Error,
        channel: 's1',
        replyTo: lastSentMessage().id,
        payload: 'Cannot subscribe to this server',
      })

      await expect(result).rejects.toThrow('Server error')
    })

    it('rejects when the ack does not arrive in time', async () => {
      const result = service.presenceSubscribe('s1', 1000)
      const assertion = expect(result).rejects.toThrow('No ack')

      vi.advanceTimersByTime(1000)

      await assertion
    })

    it('rejects when the message cannot be sent', async () => {
      socket.send.mockImplementationOnce(() => {
        throw new Error('Not connected')
      })

      await expect(service.presenceSubscribe('s1')).rejects.toThrow('Not connected')
    })
  })

  describe('presenceUnsubscribe', () => {
    it('sends a PresenceUnsubscribe with the server as channel', () => {
      void service.presenceUnsubscribe('s1')

      expect(lastSentMessage()).toEqual({
        type: WSMessageType.PresenceUnsubscribe,
        channel: 's1',
        id: expect.any(String),
      })
    })

    it('resolves with the ack', async () => {
      const result = service.presenceUnsubscribe('s1')
      receive({ type: WSMessageType.Ack, channel: 's1', replyTo: lastSentMessage().id })

      await expect(result).resolves.toMatchObject({ type: WSMessageType.Ack })
    })

    it('rejects when the connection is closed before the ack', async () => {
      const result = service.presenceUnsubscribe('s1')
      const assertion = expect(result).rejects.toThrow('Connection closed')

      handlers().onClose(new CloseEvent('close'))
      await flushPromises()

      await assertion
    })
  })
})
