import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import websocketService, { WebSocketService, type WebSocketHandlers } from './WebSocketService.ts'

// ---------- WebSocket fake ----------

// Mimics the real lifecycle: a socket starts CONNECTING and the browser fires the
// events later. The simulate* methods play the role of the browser/server.
class FakeWebSocket {
  static readonly CONNECTING = 0
  static readonly OPEN = 1
  static readonly CLOSING = 2
  static readonly CLOSED = 3
  static instances: FakeWebSocket[] = []

  url: string
  readyState = FakeWebSocket.CONNECTING
  onopen: ((event: Event) => void) | null = null
  onclose: ((event: Event) => void) | null = null
  onerror: ((event: Event) => void) | null = null
  onmessage: ((event: MessageEvent) => void) | null = null
  send = vi.fn()
  close = vi.fn(() => {
    this.readyState = FakeWebSocket.CLOSING
  })

  constructor(url: string) {
    this.url = url
    FakeWebSocket.instances.push(this)
  }

  simulateOpen() {
    this.readyState = FakeWebSocket.OPEN
    const event = new Event('open')
    this.onopen?.(event)
    return event
  }

  simulateMessage(data: string) {
    const event = new MessageEvent('message', { data })
    this.onmessage?.(event)
    return event
  }

  simulateError() {
    const event = new Event('error')
    this.onerror?.(event)
    return event
  }

  // Closed by the server or the network, or the async onclose after close()
  simulateClose() {
    this.readyState = FakeWebSocket.CLOSED
    const event = new Event('close')
    this.onclose?.(event)
    return event
  }
}

// ---------- helpers ----------

const URL_1 = 'wss://test-1'
const URL_2 = 'wss://test-2'

function socket(index = FakeWebSocket.instances.length - 1): FakeWebSocket {
  const instance = FakeWebSocket.instances[index]
  if (!instance) throw new Error(`No WebSocket was created at index ${index}`)
  return instance
}

function makeHandlers() {
  return {
    onOpen: vi.fn(),
    onClose: vi.fn(),
    onError: vi.fn(),
    onMessage: vi.fn(),
  } satisfies Required<WebSocketHandlers>
}

// ---------- tests ----------

describe('WebSocketService', () => {
  let service: WebSocketService

  beforeEach(() => {
    FakeWebSocket.instances = []
    vi.stubGlobal('WebSocket', FakeWebSocket)
    service = new WebSocketService()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  describe('connect', () => {
    it('creates a socket with the given url', () => {
      service.connect(URL_1)

      expect(FakeWebSocket.instances).toHaveLength(1)
      expect(socket().url).toBe(URL_1)
    })

    it('does not call onOpen until the socket opens', () => {
      const handlers = makeHandlers()

      service.connect(URL_1, handlers)

      expect(handlers.onOpen).not.toHaveBeenCalled()
    })

    it.each([
      { handler: 'onOpen', simulate: (s: FakeWebSocket) => s.simulateOpen() },
      { handler: 'onMessage', simulate: (s: FakeWebSocket) => s.simulateMessage('data') },
      { handler: 'onError', simulate: (s: FakeWebSocket) => s.simulateError() },
      { handler: 'onClose', simulate: (s: FakeWebSocket) => s.simulateClose() },
    ] as const)('calls $handler with the event', ({ handler, simulate }) => {
      const handlers = makeHandlers()
      service.connect(URL_1, handlers)

      const event = simulate(socket())

      expect(handlers[handler]).toHaveBeenCalledTimes(1)
      expect(handlers[handler]).toHaveBeenCalledWith(event)
    })

    it('passes the received data to onMessage', () => {
      const handlers = makeHandlers()
      service.connect(URL_1, handlers)

      socket().simulateMessage('hello from server')

      expect(handlers.onMessage.mock.lastCall?.[0].data).toBe('hello from server')
    })

    it('does not fail when no handlers are given', () => {
      service.connect(URL_1)

      expect(() => {
        socket().simulateOpen()
        socket().simulateMessage('data')
        socket().simulateError()
        socket().simulateClose()
      }).not.toThrow()
    })

    it('only calls the handlers that are given', () => {
      const onMessage = vi.fn()
      service.connect(URL_1, { onMessage })

      expect(() => socket().simulateOpen()).not.toThrow()
      socket().simulateMessage('data')

      expect(onMessage).toHaveBeenCalledTimes(1)
    })
  })

  describe('reconnecting', () => {
    it('closes the previous socket and creates a new one', () => {
      service.connect(URL_1)
      service.connect(URL_2)

      expect(socket(0).close).toHaveBeenCalledTimes(1)
      expect(FakeWebSocket.instances).toHaveLength(2)
      expect(socket(1).url).toBe(URL_2)
    })

    it('does not close the new socket', () => {
      service.connect(URL_1)
      service.connect(URL_2)

      expect(socket(1).close).not.toHaveBeenCalled()
    })

    it('does not pass the events of the previous socket to any handler', () => {
      const oldHandlers = makeHandlers()
      const newHandlers = makeHandlers()
      service.connect(URL_1, oldHandlers)
      socket(0).simulateOpen()

      service.connect(URL_2, newHandlers)
      // the browser fires these on the old socket after close()
      socket(0).simulateMessage('late message')
      socket(0).simulateError()
      socket(0).simulateClose()

      expect(oldHandlers.onMessage).not.toHaveBeenCalled()
      expect(oldHandlers.onError).not.toHaveBeenCalled()
      expect(oldHandlers.onClose).not.toHaveBeenCalled()
      expect(newHandlers.onMessage).not.toHaveBeenCalled()
      expect(newHandlers.onError).not.toHaveBeenCalled()
      expect(newHandlers.onClose).not.toHaveBeenCalled()
    })

    it('passes the events of the new socket to the new handlers', () => {
      const newHandlers = makeHandlers()
      service.connect(URL_1, makeHandlers())
      service.connect(URL_2, newHandlers)

      socket(1).simulateOpen()
      socket(1).simulateMessage('data')

      expect(newHandlers.onOpen).toHaveBeenCalledTimes(1)
      expect(newHandlers.onMessage).toHaveBeenCalledTimes(1)
    })

    it('sends through the new socket', () => {
      service.connect(URL_1)
      socket(0).simulateOpen()
      service.connect(URL_2)
      socket(1).simulateOpen()

      service.send('hello')

      expect(socket(1).send).toHaveBeenCalledWith('hello')
      expect(socket(0).send).not.toHaveBeenCalled()
    })
  })

  describe('send', () => {
    it('sends the data when the socket is open', () => {
      service.connect(URL_1)
      socket().simulateOpen()

      service.send('hello')

      expect(socket().send).toHaveBeenCalledWith('hello')
    })

    it('throws when it has never connected', () => {
      expect(() => service.send('hello')).toThrow('WebSocket is not connected')
    })

    it('throws while the socket is still connecting', () => {
      service.connect(URL_1)

      expect(() => service.send('hello')).toThrow('WebSocket is not connected')
      expect(socket().send).not.toHaveBeenCalled()
    })

    it('throws while the socket is closing', () => {
      service.connect(URL_1)
      socket().simulateOpen()
      socket().readyState = FakeWebSocket.CLOSING

      expect(() => service.send('hello')).toThrow('WebSocket is not connected')
    })

    it('throws after the server closes the connection', () => {
      service.connect(URL_1)
      socket().simulateOpen()
      socket().simulateClose()

      expect(() => service.send('hello')).toThrow('WebSocket is not connected')
      expect(socket().send).not.toHaveBeenCalled()
    })

    it('throws after close() is called', () => {
      service.connect(URL_1)
      socket().simulateOpen()
      service.close()

      expect(() => service.send('hello')).toThrow('WebSocket is not connected')
      expect(socket().send).not.toHaveBeenCalled()
    })
  })

  describe('close', () => {
    it('closes the socket', () => {
      service.connect(URL_1)

      service.close()

      expect(socket().close).toHaveBeenCalledTimes(1)
    })

    it('does not call onClose for an intentional close', () => {
      const handlers = makeHandlers()
      service.connect(URL_1, handlers)
      socket().simulateOpen()

      service.close()
      socket().simulateClose() // async onclose fired by the browser

      expect(handlers.onClose).not.toHaveBeenCalled()
    })

    it('does not pass messages received after close() to onMessage', () => {
      const handlers = makeHandlers()
      service.connect(URL_1, handlers)
      socket().simulateOpen()

      service.close()
      socket().simulateMessage('late message')

      expect(handlers.onMessage).not.toHaveBeenCalled()
    })

    it('does nothing when it has never connected', () => {
      expect(() => service.close()).not.toThrow()
    })

    it('only closes the socket once when called twice', () => {
      service.connect(URL_1)

      service.close()
      service.close()

      expect(socket().close).toHaveBeenCalledTimes(1)
    })

    it('can connect again after closing', () => {
      const handlers = makeHandlers()
      service.connect(URL_1)
      service.close()

      service.connect(URL_2, handlers)
      socket().simulateOpen()

      expect(FakeWebSocket.instances).toHaveLength(2)
      expect(handlers.onOpen).toHaveBeenCalledTimes(1)
    })
  })

  describe('isConnected', () => {
    it('returns false when it has never connected', () => {
      expect(service.isConnected()).toBe(false)
    })

    it('returns false while the socket is connecting', () => {
      service.connect(URL_1)

      expect(service.isConnected()).toBe(false)
    })

    it('returns true when the socket is open', () => {
      service.connect(URL_1)
      socket().simulateOpen()

      expect(service.isConnected()).toBe(true)
    })

    it('returns false after the server closes the connection', () => {
      service.connect(URL_1)
      socket().simulateOpen()
      socket().simulateClose()

      expect(service.isConnected()).toBe(false)
    })

    it('returns false after close() is called', () => {
      service.connect(URL_1)
      socket().simulateOpen()
      service.close()

      expect(service.isConnected()).toBe(false)
    })
  })

  describe('default export', () => {
    it('is a WebSocketService instance', () => {
      expect(websocketService).toBeInstanceOf(WebSocketService)
    })
  })
})
