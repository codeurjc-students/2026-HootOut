import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { WebSocketMessageService } from '@/services/common/websocketsMesage/WebSocketsMessageService'
import type { WebSocketHandlers } from '@/services/common/websockets/WebSocketService'
import { WSMessageType, type ChatChannel, type WebSocketMessage } from '@/types'

// ---------- mocks ----------

// vi.hoisted keeps the same mock objects even when the modules are reloaded
// with vi.resetModules() in the reconnection tests
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

const RECONNECT_INTERVAL = 3000
const channel: ChatChannel = {
  id: 'general',
  createdAt: '',
  modifiedAt: '',
  serverId: 'test',
  name: 'test',
}

// Handlers passed to webSocketService.connect() in the last connection
function handlers(): Required<WebSocketHandlers> {
  const call = socket.connect.mock.lastCall
  if (!call) throw new Error('webSocketService.connect() was not called')
  return call[1]
}

// Connects the service and simulates the socket opening
async function connectService(service: WebSocketMessageService, onOpen = vi.fn(async () => {})) {
  await service.connect(onOpen)
  await handlers().onOpen(new Event('open'))
  return onOpen
}

function receive(message: WebSocketMessage) {
  handlers().onMessage(new MessageEvent('message', { data: JSON.stringify(message) }))
}

function receiveRaw(data: string) {
  handlers().onMessage(new MessageEvent('message', { data }))
}

// Messages sent to the server, parsed
const sentMessages = (): WebSocketMessage[] =>
  socket.send.mock.calls.map(([data]) => JSON.parse(data))
const lastSentMessage = () => sentMessages().at(-1)!

// ---------- tests ----------

describe('WebSocketMessageService', () => {
  let service: WebSocketMessageService

  beforeEach(() => {
    // Only the timers are faked: flushPromises still works with real setImmediate
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'] })
    vi.resetAllMocks()
    socket.isConnected.mockReturnValue(false)
    api.post.mockResolvedValue({ ticket: 'ticket-1' })
    service = new WebSocketMessageService()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('connect', () => {
    it('requests a ticket and connects to the websocket url with it', async () => {
      await service.connect(vi.fn())

      expect(api.post).toHaveBeenCalledWith('auth/ws-ticket')
      expect(socket.connect).toHaveBeenCalledWith('wss://test?ticket=ticket-1', expect.any(Object))
    })

    it('marks the service as active', async () => {
      await service.connect(vi.fn())

      expect(service.isActive).toBe(true)
    })

    it('is not connected until the socket opens', async () => {
      const onOpen = vi.fn()

      await service.connect(onOpen)

      expect(service.connected.value).toBe(false)
      expect(onOpen).not.toHaveBeenCalled()
    })

    it('is connected and calls the callback when the socket opens', async () => {
      const onOpen = await connectService(service)

      expect(service.connected.value).toBe(true)
      expect(onOpen).toHaveBeenCalledTimes(1)
    })

    it('only calls the callback when the socket is already connected', async () => {
      socket.isConnected.mockReturnValue(true)
      const onOpen = vi.fn(async () => {})

      await service.connect(onOpen)

      expect(onOpen).toHaveBeenCalledTimes(1)
      expect(api.post).not.toHaveBeenCalled()
      expect(socket.connect).not.toHaveBeenCalled()
    })

    it('fails and does not connect when the ticket request fails', async () => {
      api.post.mockRejectedValueOnce(new Error('HTTP 401'))

      await expect(service.connect(vi.fn())).rejects.toThrow('HTTP 401')
      expect(socket.connect).not.toHaveBeenCalled()
    })

    it.each(['onClose', 'onError'] as const)('is not connected after %s', async (handler) => {
      await connectService(service)

      handlers()[handler](new Event(handler))

      expect(service.connected.value).toBe(false)
    })
  })

  describe('subscribe', () => {
    beforeEach(async () => {
      await connectService(service)
    })

    it('sends a Subscribe message for the channel', () => {
      service.subscribe('general', vi.fn())

      expect(lastSentMessage()).toEqual({ channel: 'general', type: WSMessageType.Subscribe })
    })

    it('delivers the messages of the channel to the callback', () => {
      const callback = vi.fn()
      service.subscribe('general', callback)
      const message: WebSocketMessage = {
        channel: 'general',
        type: WSMessageType.ChatMessage,
        payload: '"hi"',
      }

      receive(message)

      expect(callback).toHaveBeenCalledTimes(1)
      expect(callback).toHaveBeenCalledWith(message)
    })

    it('does not deliver messages of other channels', () => {
      const callback = vi.fn()
      service.subscribe('general', callback)

      receive({ channel: 'random', type: WSMessageType.ChatMessage })

      expect(callback).not.toHaveBeenCalled()
    })

    it('delivers each message only to the callback of its channel', () => {
      const general = vi.fn()
      const random = vi.fn()
      service.subscribe('general', general)
      service.subscribe('random', random)

      receive({ channel: 'random', type: WSMessageType.ChatMessage })

      expect(general).not.toHaveBeenCalled()
      expect(random).toHaveBeenCalledTimes(1)
    })

    it('replaces the callback when subscribing twice to the same channel', () => {
      const first = vi.fn()
      const second = vi.fn()
      service.subscribe('general', first)
      service.subscribe('general', second)

      receive({ channel: 'general', type: WSMessageType.ChatMessage })

      expect(first).not.toHaveBeenCalled()
      expect(second).toHaveBeenCalledTimes(1)
    })

    it('ignores messages of channels without a subscription', () => {
      expect(() => receive({ channel: 'unknown', type: WSMessageType.ChatMessage })).not.toThrow()
    })

    it('ignores messages that are not valid JSON', () => {
      const callback = vi.fn()
      service.subscribe('general', callback)

      expect(() => receiveRaw('not json')).not.toThrow()
      expect(callback).not.toHaveBeenCalled()
    })

    it('throws when the socket is not connected', () => {
      socket.send.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })

      expect(() => service.subscribe('general', vi.fn())).toThrow('WebSocket is not connected')
    })
  })

  describe('unsubscribe', () => {
    beforeEach(async () => {
      await connectService(service)
    })

    it('sends an Unsubscribe message for the channel', () => {
      const callback = vi.fn()
      service.subscribe('general', callback)

      service.unsubscribe('general', callback)

      expect(lastSentMessage()).toEqual({ channel: 'general', type: WSMessageType.Unsubscribe })
    })

    it('stops delivering the messages of the channel', () => {
      const callback = vi.fn()
      service.subscribe('general', callback)

      service.unsubscribe('general', callback)
      receive({ channel: 'general', type: WSMessageType.ChatMessage })

      expect(callback).not.toHaveBeenCalled()
    })

    it('keeps the other subscriptions', () => {
      const random = vi.fn()
      service.subscribe('general', vi.fn())
      service.subscribe('random', random)

      service.unsubscribe('general', vi.fn())
      receive({ channel: 'random', type: WSMessageType.ChatMessage })

      expect(random).toHaveBeenCalledTimes(1)
    })
  })

  describe('sendMessage', () => {
    beforeEach(async () => {
      await connectService(service)
    })

    it('sends a ChatMessage to the channel with the payload as JSON and an id', () => {
      const { id } = service.sendMessage(channel, { text: 'hello' })

      expect(lastSentMessage()).toEqual({
        id,
        channel: 'general',
        type: WSMessageType.ChatMessage,
        payload: JSON.stringify({ text: 'hello' }),
      })
    })

    it('gives each message a different id', () => {
      const first = service.sendMessage(channel, 'a')
      const second = service.sendMessage(channel, 'b')

      expect(first.id).toBeTruthy()
      expect(first.id).not.toBe(second.id)
    })

    it('resolves with the ack that replies to the message', async () => {
      const { id, promise } = service.sendMessage(channel, 'hello')
      const ack: WebSocketMessage = { replyTo: id, channel: 'general', type: WSMessageType.Ack }

      receive(ack)

      await expect(promise).resolves.toEqual(ack)
    })

    it('does not deliver the ack to the subscribers of the channel', async () => {
      const callback = vi.fn()
      service.subscribe('general', callback)
      const { id, promise } = service.sendMessage(channel, 'hello')

      receive({ replyTo: id, channel: 'general', type: WSMessageType.Ack })
      await promise

      expect(callback).not.toHaveBeenCalled()
    })

    it('only resolves the message the ack replies to', async () => {
      const first = service.sendMessage(channel, 'a')
      const second = service.sendMessage(channel, 'b')
      const onSecond = vi.fn()
      second.promise.then(onSecond)

      receive({ replyTo: first.id, type: WSMessageType.Ack, channel: channel.id })
      await first.promise
      await flushPromises()

      expect(onSecond).not.toHaveBeenCalled()
    })

    it('rejects when the server answers with an error', async () => {
      const { id, promise } = service.sendMessage(channel, 'hello')

      receive({ replyTo: id, channel: 'general', type: WSMessageType.Error })

      await expect(promise).rejects.toThrow('Server error')
    })

    it('does not deliver an error ack to the subscribers of the channel', async () => {
      const callback = vi.fn()
      service.subscribe('general', callback)
      const { id, promise } = service.sendMessage(channel, 'hello')

      receive({ replyTo: id, channel: 'general', type: WSMessageType.Error })
      await expect(promise).rejects.toThrow()

      expect(callback).not.toHaveBeenCalled()
    })

    it('delivers a message that replies to an unknown id to the channel subscribers', () => {
      const callback = vi.fn()
      service.subscribe('general', callback)

      receive({ replyTo: 'unknown-id', channel: 'general', type: WSMessageType.ChatMessage })

      expect(callback).toHaveBeenCalledTimes(1)
    })

    it('rejects when no ack arrives within the default 5s', async () => {
      const { promise } = service.sendMessage(channel, 'hello')
      const assertion = expect(promise).rejects.toThrow(
        `No ack for ${WSMessageType.ChatMessage} within 5000ms`
      )

      await vi.advanceTimersByTimeAsync(4999)
      const settled = vi.fn()
      promise.then(settled, settled)
      await flushPromises()
      expect(settled).not.toHaveBeenCalled()

      await vi.advanceTimersByTimeAsync(1)
      await assertion
    })

    it('uses a custom timeout', async () => {
      const { promise } = service.sendMessage(channel, 'hello', 1000)
      const assertion = expect(promise).rejects.toThrow('within 1000ms')

      await vi.advanceTimersByTimeAsync(1000)

      await assertion
    })

    it('cancels the timeout when the ack arrives', async () => {
      const { id, promise } = service.sendMessage(channel, 'hello')

      receive({ replyTo: id, type: WSMessageType.Ack, channel: channel.id })
      await promise

      expect(vi.getTimerCount()).toBe(0)
    })

    it('rejects the pending messages when the connection closes', async () => {
      const first = service.sendMessage(channel, 'a')
      const second = service.sendMessage(channel, 'b')

      handlers().onClose(new Event('close'))

      await expect(first.promise).rejects.toThrow('Connection closed')
      await expect(second.promise).rejects.toThrow('Connection closed')
      expect(vi.getTimerCount()).toBe(0)
    })

    it('rejects with the send error when the socket is not connected', async () => {
      socket.send.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })

      const { promise } = service.sendMessage(channel, 'hello')

      await expect(promise).rejects.toThrow('WebSocket is not connected')
      expect(vi.getTimerCount()).toBe(0)
    })

    it('does not wait for an ack of a message that could not be sent', async () => {
      const callback = vi.fn()
      service.subscribe('general', callback)
      socket.send.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })
      const { id, promise } = service.sendMessage(channel, 'hello')
      await expect(promise).rejects.toThrow()

      // a message with that id is not an ack anymore: it goes to the subscribers
      receive({ replyTo: id, channel: 'general', type: WSMessageType.ChatMessage })

      expect(callback).toHaveBeenCalledTimes(1)
    })
  })

  describe('pending subscriptions', () => {
    it('does not send anything when adding a pending subscription', () => {
      service.addPendingSubscription('general', vi.fn())

      expect(socket.send).not.toHaveBeenCalled()
    })

    it('subscribes the pending channels', async () => {
      service.addPendingSubscription('general', vi.fn())
      service.addPendingSubscription('random', vi.fn())

      service.subscribePendingSubscriptions()

      expect(sentMessages()).toEqual([
        { channel: 'general', type: WSMessageType.Subscribe },
        { channel: 'random', type: WSMessageType.Subscribe },
      ])
    })

    it('delivers the messages of a pending channel to its callback once subscribed', async () => {
      await connectService(service)
      const callback = vi.fn()
      service.addPendingSubscription('general', callback)

      service.subscribePendingSubscriptions()
      receive({ channel: 'general', type: WSMessageType.ChatMessage })

      expect(callback).toHaveBeenCalledTimes(1)
    })

    it('subscribes the existing subscriptions again', async () => {
      await connectService(service)
      service.subscribe('general', vi.fn())
      socket.send.mockClear()

      service.subscribePendingSubscriptions()

      expect(sentMessages()).toEqual([{ channel: 'general', type: WSMessageType.Subscribe }])
    })

    it('subscribes each channel only once', async () => {
      await connectService(service)
      service.subscribe('general', vi.fn())
      service.addPendingSubscription('general', vi.fn())
      service.addPendingSubscription('random', vi.fn())
      socket.send.mockClear()

      service.subscribePendingSubscriptions()

      expect(sentMessages().map((m) => m.channel)).toEqual(['general', 'random'])
    })

    it('uses the pending callback when the channel already had one', async () => {
      await connectService(service)
      const existing = vi.fn()
      const pending = vi.fn()
      service.subscribe('general', existing)
      service.addPendingSubscription('general', pending)

      service.subscribePendingSubscriptions()
      receive({ channel: 'general', type: WSMessageType.ChatMessage })

      expect(existing).not.toHaveBeenCalled()
      expect(pending).toHaveBeenCalledTimes(1)
    })
  })

  describe('disconnect', () => {
    beforeEach(async () => {
      await connectService(service)
    })

    it('closes the socket', () => {
      service.disconnect()

      expect(socket.close).toHaveBeenCalledTimes(1)
    })

    it('is not connected nor active anymore', () => {
      service.disconnect()

      expect(service.connected.value).toBe(false)
      expect(service.isActive).toBe(false)
    })

    it('rejects the pending messages', async () => {
      const { promise } = service.sendMessage(channel, 'hello')

      service.disconnect()

      await expect(promise).rejects.toThrow('Connection closed')
    })
  })

  describe('tryReconnect', () => {
    it('connects again and resubscribes the channels when the socket opens', async () => {
      await connectService(service)
      const callback = vi.fn()
      service.subscribe('general', callback)
      handlers().onClose(new Event('close'))
      socket.connect.mockClear()
      socket.send.mockClear()

      await service.tryReconnect()
      await handlers().onOpen(new Event('open'))

      expect(socket.connect).toHaveBeenCalledTimes(1)
      expect(sentMessages()).toEqual([{ channel: 'general', type: WSMessageType.Subscribe }])
      receive({ channel: 'general', type: WSMessageType.ChatMessage })
      expect(callback).toHaveBeenCalledTimes(1)
    })

    it('does nothing when it is connected', async () => {
      await connectService(service)
      socket.connect.mockClear()

      await service.tryReconnect()

      expect(socket.connect).not.toHaveBeenCalled()
    })

    it('makes the first connection when it has never been connected', async () => {
      await service.tryReconnect()
      await handlers().onOpen(new Event('open'))

      expect(socket.connect).toHaveBeenCalledTimes(1)
      expect(service.connected.value).toBe(true)
      expect(service.isActive).toBe(true)
    })

    it('connects again after disconnect() when called explicitly (e.g. a new login)', async () => {
      await connectService(service)
      service.disconnect()
      socket.connect.mockClear()

      await service.tryReconnect()

      expect(socket.connect).toHaveBeenCalledTimes(1)
      expect(service.isActive).toBe(true)
    })
  })

  describe('automatic reconnection', () => {
    let wsMessageService: WebSocketMessageService
    let firstConnection: Required<WebSocketHandlers>

    // The reconnection logic lives at module level and watches the default instance,
    // so each test loads a fresh copy of the module
    beforeEach(async () => {
      vi.resetModules()
      const module = await import('@/services/common/websocketsMesage/WebSocketsMessageService')
      wsMessageService = module.default
      await connectService(wsMessageService)
      firstConnection = handlers()
      socket.connect.mockClear() // from now on, only the reconnection attempts are counted
    })

    afterEach(() => {
      wsMessageService.disconnect() // stops any reconnection loop
    })

    async function loseConnection() {
      firstConnection.onClose(new Event('close'))
      await flushPromises()
    }

    it('tries to reconnect as soon as the connection is lost', async () => {
      await loseConnection()

      expect(socket.connect).toHaveBeenCalledTimes(1)
    })

    it('keeps trying every 3 seconds while it is not connected', async () => {
      await loseConnection()

      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL)
      expect(socket.connect).toHaveBeenCalledTimes(2)

      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL)
      expect(socket.connect).toHaveBeenCalledTimes(3)
    })

    it('stops trying once it is connected again', async () => {
      await loseConnection()

      await handlers().onOpen(new Event('open'))
      await flushPromises()
      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL * 3)

      expect(socket.connect).toHaveBeenCalledTimes(1)
      expect(wsMessageService.connected.value).toBe(true)
    })

    it('keeps trying when the first attempt fails', async () => {
      api.post.mockRejectedValueOnce(new Error('Network error'))

      await loseConnection()
      expect(socket.connect).not.toHaveBeenCalled()

      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL)
      expect(socket.connect).toHaveBeenCalledTimes(1)
    })

    it('keeps trying when an attempt in the interval fails', async () => {
      await loseConnection()
      api.post.mockRejectedValueOnce(new Error('Network error'))

      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL) // fails
      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL)

      expect(socket.connect).toHaveBeenCalledTimes(2)
    })

    it('does not reconnect after disconnect()', async () => {
      wsMessageService.disconnect()
      await flushPromises()
      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL * 3)

      expect(socket.connect).not.toHaveBeenCalled()
    })

    it('stops trying when disconnect() is called while reconnecting', async () => {
      await loseConnection()
      expect(socket.connect).toHaveBeenCalledTimes(1)

      wsMessageService.disconnect()
      await vi.advanceTimersByTimeAsync(RECONNECT_INTERVAL * 3)

      expect(socket.connect).toHaveBeenCalledTimes(1)
      expect(vi.getTimerCount()).toBe(0)
    })

    it('resubscribes the channels after reconnecting', async () => {
      wsMessageService.subscribe('general', vi.fn())
      await loseConnection()
      socket.send.mockClear()

      await handlers().onOpen(new Event('open'))

      expect(sentMessages()).toEqual([{ channel: 'general', type: WSMessageType.Subscribe }])
    })
  })
})
