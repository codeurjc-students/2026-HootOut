import { watch, type Ref } from 'vue'
import type { RouteLocationNormalized, Router } from 'vue-router'
import wsMessageService from '@/services/common/websocketsMessage/WebSocketsMessageService'
import { presenceStore } from '@/stores/presence'
import { WSMessageType, type WebSocketMessage } from '@/types'

// The part of WebSocketMessageService that the presence needs
export interface PresenceSocket {
  connected: Ref<boolean>
  setTypeHandler(type: WSMessageType, handler: (message: WebSocketMessage) => void): void
  removeTypeHandler(type: WSMessageType): void
  presenceSubscribe(serverId: string): Promise<WebSocketMessage>
  presenceUnsubscribe(serverId: string): Promise<WebSocketMessage>
}

// Server of the route: the chat of a server and its settings (e.g. the active members)
export function serverIdOf(route: RouteLocationNormalized): string | undefined {
  const serverId = route.params.serverId
  return typeof serverId === 'string' && serverId ? serverId : undefined
}

/**
 * Keeps the presence store in sync with the websocket and the route:
 * - The Presence and PresenceSnapshot messages go to the store. The presence of the friends
 *   arrives on its own after connecting.
 * - While the route has a serverId, the presence of its members is subscribed. It is
 *   unsubscribed when the server changes or the route has no server.
 * - When the connection is lost the store is cleared. After reconnecting the server sends the
 *   friends again, and the server of the route is subscribed again.
 *
 * Call it once, after installing Pinia, so the snapshot of the first connection is not lost.
 * Returns a function that undoes it (used by the tests)
 */
export function setupPresence(
  router: Router,
  socket: PresenceSocket = wsMessageService
): () => void {
  const presence = presenceStore()

  socket.setTypeHandler(WSMessageType.Presence, presence.handleMessage)
  socket.setTypeHandler(WSMessageType.PresenceSnapshot, presence.handleMessage)

  async function subscribe(serverId: string) {
    try {
      await socket.presenceSubscribe(serverId)
    } catch {
      // Not a member, no ack in time or the connection was lost: the members stay offline.
      // After a reconnection it is tried again
    }
  }

  async function unsubscribe(serverId: string) {
    try {
      await socket.presenceUnsubscribe(serverId)
    } catch {
      // Without connection there is no subscription to remove. Late events of that
      // server are ignored by the store
    }
  }

  function showServer(serverId: string | undefined) {
    const previous = presence.currentServerId
    if (!presence.setCurrentServer(serverId)) return
    // Without connection the server forgot the subscriptions: it subscribes when it connects
    if (!socket.connected.value) return

    if (previous) void unsubscribe(previous)
    if (presence.currentServerId) void subscribe(presence.currentServerId)
  }

  const stopWatching = watch(socket.connected, (connected) => {
    if (!connected) {
      presence.clear()
      return
    }
    // New connection: subscribe again to the server that is shown
    if (presence.currentServerId) void subscribe(presence.currentServerId)
  })

  const removeHook = router.afterEach((to, _from, failure) => {
    if (!failure) showServer(serverIdOf(to))
  })

  // The navigation may have finished already
  showServer(serverIdOf(router.currentRoute.value))

  return () => {
    stopWatching()
    removeHook()
    socket.removeTypeHandler(WSMessageType.Presence)
    socket.removeTypeHandler(WSMessageType.PresenceSnapshot)
  }
}
