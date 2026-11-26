import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { setupPresence, type PresenceSocket } from '@/services/presence/PresenceSync'
import { presenceStore } from '@/stores/presence'
import { PresenceStatus, WSMessageType, type WebSocketMessage } from '@/types'

// The default socket is never used: every test passes its own
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({ default: {} }))

// ---------- helpers ----------

const SERVER_1 = 'server-1'
const SERVER_2 = 'server-2'
const FRIEND = 'friend-1'
const MEMBER = 'member-1'

function createSocket() {
  const handlers: Partial<Record<WSMessageType, (message: WebSocketMessage) => void>> = {}
  const socket = {
    connected: ref(false),
    setTypeHandler: vi.fn((type: WSMessageType, handler: (m: WebSocketMessage) => void) => {
      handlers[type] = handler
    }),
    removeTypeHandler: vi.fn((type: WSMessageType) => {
      delete handlers[type]
    }),
    presenceSubscribe: vi.fn<PresenceSocket['presenceSubscribe']>(async () => ({
      type: WSMessageType.Ack,
      channel: null,
    })),
    presenceUnsubscribe: vi.fn<PresenceSocket['presenceUnsubscribe']>(async () => ({
      type: WSMessageType.Ack,
      channel: null,
    })),
    // Simulates a message of the server, routed by type like WebSocketMessageService does
    receive(message: WebSocketMessage) {
      handlers[message.type as WSMessageType]?.(message)
    },
  }
  return socket
}

// Same paths as the app's router, with test components and no guards
function createTestRouter(): Router {
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
      { path: '/chat/server/:serverId?', name: 'server', component: placeholder },
      {
        path: '/chat/server/:serverId/channel/:channelId',
        name: 'channel',
        component: placeholder,
      },
      {
        path: '/settings/server/:serverId',
        name: 'serverSettings',
        component: placeholder,
        children: [{ path: 'members/active', name: 'serverActiveMembers', component: placeholder }],
      },
    ],
  })
}

function friendsSnapshot(onlineUserIds: string[]): WebSocketMessage {
  return {
    type: WSMessageType.PresenceSnapshot,
    channel: 'friends',
    payload: JSON.stringify({ OnlineUserIds: onlineUserIds }),
  }
}

function serverSnapshot(serverId: string, onlineUserIds: string[]): WebSocketMessage {
  return {
    type: WSMessageType.PresenceSnapshot,
    channel: serverId,
    payload: JSON.stringify({ OnlineUserIds: onlineUserIds }),
  }
}

function friendPresence(userId: string, status: PresenceStatus): WebSocketMessage {
  return {
    type: WSMessageType.Presence,
    channel: `presence.user.${userId}`,
    payload: JSON.stringify({ UserId: userId, Status: status }),
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

// ---------- tests ----------

describe('setupPresence', () => {
  let router: Router
  let socket: ReturnType<typeof createSocket>
  let teardown: (() => void) | undefined

  async function navigate(path: string) {
    await router.push(path)
    await flushPromises()
  }

  async function setConnected(connected: boolean) {
    socket.connected.value = connected
    await flushPromises()
  }

  // Connected on /chat, as after opening the app
  async function start(path = '/chat', connected = true) {
    await router.push(path)
    await router.isReady()
    socket.connected.value = connected
    teardown = setupPresence(router, socket)
    await flushPromises()
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    router = createTestRouter()
    socket = createSocket()
  })

  afterEach(() => {
    teardown?.()
    teardown = undefined
  })

  describe('messages', () => {
    it('handles the Presence and PresenceSnapshot messages', async () => {
      await start()

      expect(socket.setTypeHandler).toHaveBeenCalledWith(
        WSMessageType.Presence,
        expect.any(Function)
      )
      expect(socket.setTypeHandler).toHaveBeenCalledWith(
        WSMessageType.PresenceSnapshot,
        expect.any(Function)
      )
    })

    it('passes the snapshot of the friends and their events to the store', async () => {
      await start()
      const presence = presenceStore()

      socket.receive(friendsSnapshot([FRIEND]))
      expect(presence.isOnline(FRIEND)).toBe(true)

      socket.receive(friendPresence(FRIEND, PresenceStatus.Offline))
      expect(presence.isOnline(FRIEND)).toBe(false)
    })

    it('stops handling them after the teardown', async () => {
      await start()

      teardown!()
      teardown = undefined

      expect(socket.removeTypeHandler).toHaveBeenCalledWith(WSMessageType.Presence)
      expect(socket.removeTypeHandler).toHaveBeenCalledWith(WSMessageType.PresenceSnapshot)
    })
  })

  describe('server of the route', () => {
    it('does not subscribe outside a server', async () => {
      await start('/chat/direct')

      expect(presenceStore().currentServerId).toBeUndefined()
      expect(socket.presenceSubscribe).not.toHaveBeenCalled()
    })

    it('subscribes to the server of the route when it starts', async () => {
      await start(`/chat/server/${SERVER_1}`)

      expect(presenceStore().currentServerId).toBe(SERVER_1)
      expect(socket.presenceSubscribe).toHaveBeenCalledExactlyOnceWith(SERVER_1)
    })

    it('subscribes when a server is opened', async () => {
      await start()

      await navigate(`/chat/server/${SERVER_1}`)

      expect(socket.presenceSubscribe).toHaveBeenCalledExactlyOnceWith(SERVER_1)
      expect(socket.presenceUnsubscribe).not.toHaveBeenCalled()
    })

    it('does not subscribe again when the channel changes inside the same server', async () => {
      await start(`/chat/server/${SERVER_1}`)

      await navigate(`/chat/server/${SERVER_1}/channel/c1`)
      await navigate(`/chat/server/${SERVER_1}/channel/c2`)

      expect(socket.presenceSubscribe).toHaveBeenCalledOnce()
      expect(socket.presenceUnsubscribe).not.toHaveBeenCalled()
    })

    it('keeps the subscription in the settings of the same server', async () => {
      await start(`/chat/server/${SERVER_1}`)

      await navigate(`/settings/server/${SERVER_1}/members/active`)

      expect(socket.presenceSubscribe).toHaveBeenCalledOnce()
      expect(socket.presenceUnsubscribe).not.toHaveBeenCalled()
    })

    it('subscribes in the settings of a server opened from its url', async () => {
      await start(`/settings/server/${SERVER_1}/members/active`)

      expect(socket.presenceSubscribe).toHaveBeenCalledExactlyOnceWith(SERVER_1)
    })

    it('unsubscribes from the previous server and subscribes to the new one', async () => {
      await start(`/chat/server/${SERVER_1}`)

      await navigate(`/chat/server/${SERVER_2}`)

      expect(socket.presenceUnsubscribe).toHaveBeenCalledExactlyOnceWith(SERVER_1)
      expect(socket.presenceSubscribe).toHaveBeenLastCalledWith(SERVER_2)
    })

    it('unsubscribes when leaving the server', async () => {
      await start(`/chat/server/${SERVER_1}`)

      await navigate('/chat/direct')

      expect(socket.presenceUnsubscribe).toHaveBeenCalledExactlyOnceWith(SERVER_1)
      expect(presenceStore().currentServerId).toBeUndefined()
    })

    it('shows the members of the snapshot of the server', async () => {
      await start(`/chat/server/${SERVER_1}`)

      socket.receive(serverSnapshot(SERVER_1, [MEMBER]))

      expect(presenceStore().isOnline(MEMBER)).toBe(true)
    })

    it('forgets the members of the previous server', async () => {
      await start(`/chat/server/${SERVER_1}`)
      socket.receive(serverSnapshot(SERVER_1, [MEMBER]))

      await navigate(`/chat/server/${SERVER_2}`)

      expect(presenceStore().isOnline(MEMBER)).toBe(false)
    })

    it('ignores the snapshot of a server that was left while subscribing', async () => {
      const subscribe = deferred<WebSocketMessage>()
      socket.presenceSubscribe.mockReturnValueOnce(subscribe.promise)
      await start(`/chat/server/${SERVER_1}`)

      await navigate(`/chat/server/${SERVER_2}`)
      socket.receive(serverSnapshot(SERVER_1, [MEMBER]))
      subscribe.resolve({ type: WSMessageType.Ack, channel: null })
      await flushPromises()

      expect(presenceStore().isOnline(MEMBER)).toBe(false)
    })

    it('does nothing after a failed navigation', async () => {
      await start(`/chat/server/${SERVER_1}`)
      router.beforeEach((to) => (to.params.serverId === SERVER_2 ? false : undefined))

      await navigate(`/chat/server/${SERVER_2}`)

      expect(presenceStore().currentServerId).toBe(SERVER_1)
      expect(socket.presenceUnsubscribe).not.toHaveBeenCalled()
    })
  })

  describe('errors', () => {
    it('catches a rejected subscription (e.g. not a member of the server)', async () => {
      socket.presenceSubscribe.mockRejectedValue(new Error('Server error'))

      await start(`/chat/server/${SERVER_1}`)

      // Vitest fails the run on unhandled rejections
      expect(socket.presenceSubscribe).toHaveBeenCalledOnce()
      expect(presenceStore().currentServerId).toBe(SERVER_1)
    })

    it('catches a rejected unsubscription', async () => {
      socket.presenceUnsubscribe.mockRejectedValue(new Error('Connection closed'))
      await start(`/chat/server/${SERVER_1}`)

      await navigate('/chat')

      expect(socket.presenceUnsubscribe).toHaveBeenCalledOnce()
    })
  })

  describe('connection', () => {
    it('does not send anything while disconnected', async () => {
      await start(`/chat/server/${SERVER_1}`, false)

      await navigate(`/chat/server/${SERVER_2}`)

      expect(socket.presenceSubscribe).not.toHaveBeenCalled()
      expect(socket.presenceUnsubscribe).not.toHaveBeenCalled()
      expect(presenceStore().currentServerId).toBe(SERVER_2)
    })

    it('subscribes to the server of the route when it connects', async () => {
      await start(`/chat/server/${SERVER_1}`, false)

      await setConnected(true)

      expect(socket.presenceSubscribe).toHaveBeenCalledExactlyOnceWith(SERVER_1)
    })

    it('does not subscribe when it connects outside a server', async () => {
      await start('/chat', false)

      await setConnected(true)

      expect(socket.presenceSubscribe).not.toHaveBeenCalled()
    })

    it('forgets every status when the connection is lost', async () => {
      await start(`/chat/server/${SERVER_1}`)
      socket.receive(friendsSnapshot([FRIEND]))
      socket.receive(serverSnapshot(SERVER_1, [MEMBER]))

      await setConnected(false)

      expect(presenceStore().statuses).toEqual({})
      expect(presenceStore().currentServerId).toBe(SERVER_1)
    })

    it('subscribes again to the server after reconnecting', async () => {
      await start(`/chat/server/${SERVER_1}`)

      await setConnected(false)
      await setConnected(true)

      expect(socket.presenceSubscribe).toHaveBeenCalledTimes(2)
      expect(socket.presenceSubscribe).toHaveBeenLastCalledWith(SERVER_1)
    })

    it('subscribes after reconnecting to the server opened while disconnected', async () => {
      await start(`/chat/server/${SERVER_1}`)
      await setConnected(false)

      await navigate(`/chat/server/${SERVER_2}`)
      await setConnected(true)

      // The server forgot the subscription to the first one: nothing to unsubscribe
      expect(socket.presenceUnsubscribe).not.toHaveBeenCalled()
      expect(socket.presenceSubscribe).toHaveBeenLastCalledWith(SERVER_2)
    })

    it('shows the presence again with the snapshots of the new connection', async () => {
      await start(`/chat/server/${SERVER_1}`)
      await setConnected(false)
      await setConnected(true)

      socket.receive(friendsSnapshot([FRIEND]))
      socket.receive(serverSnapshot(SERVER_1, [MEMBER]))

      expect(presenceStore().isOnline(FRIEND)).toBe(true)
      expect(presenceStore().isOnline(MEMBER)).toBe(true)
    })
  })
})
