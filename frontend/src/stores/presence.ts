import { defineStore } from 'pinia'
import { ref } from 'vue'
import { PresenceStatus, WSMessageType, type WebSocketMessage } from '@/types'

// Channel of the snapshot of the friends that the server sends right after connecting
export const FRIENDS_SNAPSHOT_CHANNEL = 'friends'
// Channels of the presence events: one per user (friends) and one per server
export const USER_PRESENCE_PREFIX = 'presence.user.'
export const SERVER_PRESENCE_PREFIX = 'presence.server.'

// The backend sends the guids in lowercase; compare them without depending on it
function normalizeId(id: string): string {
  return id.toLowerCase()
}

// The payload is a JSON string. Returns null when it is missing or is not a JSON object
function parsePayload(payload: unknown): Record<string, unknown> | null {
  try {
    const value = typeof payload === 'string' ? JSON.parse(payload) : payload
    return value && typeof value === 'object' ? (value as Record<string, unknown>) : null
  } catch {
    return null
  }
}

// The fields can arrive in camelCase or in PascalCase, depending on how the backend serializes them
function field(data: Record<string, unknown>, name: string): unknown {
  return data[name] ?? data[name.charAt(0).toUpperCase() + name.slice(1)]
}

// Accepts the number of the enum (10 / 0) or its name ("Online" / "Offline")
function parseStatus(value: unknown): PresenceStatus | null {
  if (value === PresenceStatus.Online || value === PresenceStatus.Offline) return value
  if (typeof value === 'string') {
    const name = value.toLowerCase()
    if (name === 'online') return PresenceStatus.Online
    if (name === 'offline') return PresenceStatus.Offline
  }
  return null
}

// Presence of the members of the current server, for the views that keep their own lists of
// members (e.g. the online and offline lists of the chat). The ids are normalized
export type ServerPresenceEvent =
  | { type: 'presence'; serverId: string; userId: string; status: PresenceStatus }
  | { type: 'snapshot'; serverId: string; onlineUserIds: string[] }

export type ServerPresenceListener = (event: ServerPresenceEvent) => void

export const presenceStore = defineStore('presence', () => {
  // userId -> status. Only the online users are kept: a user that is not here is offline
  const statuses = ref<Record<string, PresenceStatus>>({})

  // Server whose members are being followed (the server of the route), already normalized
  const currentServerId = ref<string>()

  // Users followed through their own channel (the friends, from the snapshot of the friends and
  // from presence.user.<id>). Their status stays up to date when the server changes.
  // It is not returned by the store: nothing outside needs it
  let friends = new Set<string>()

  // Not reactive on purpose: they are callbacks, not state
  const serverListeners = new Set<ServerPresenceListener>()

  /** Calls the listener with every event and snapshot of the current server. Returns a function that removes it */
  function onServerPresence(listener: ServerPresenceListener): () => void {
    serverListeners.add(listener)
    return () => serverListeners.delete(listener)
  }

  function notifyServer(event: ServerPresenceEvent) {
    for (const listener of [...serverListeners]) {
      try {
        listener(event)
      } catch {
        // A broken listener must not stop the presence of the rest
      }
    }
  }

  function isOnline(userId: string | null | undefined): boolean {
    if (!userId) return false
    return statuses.value[normalizeId(userId)] === PresenceStatus.Online
  }

  function setStatus(userId: string, status: PresenceStatus) {
    if (status === PresenceStatus.Online) statuses.value[userId] = PresenceStatus.Online
    else delete statuses.value[userId]
  }

  function isCurrentServer(serverId: string): boolean {
    return !!currentServerId.value && normalizeId(serverId) === currentServerId.value
  }

  // A user went online or offline. Repeated events are harmless (e.g. an "online" after
  // reconnecting inside the grace period of the backend)
  function handlePresence(message: WebSocketMessage) {
    const channel = message.channel ?? ''
    const isFriendChannel = channel.startsWith(USER_PRESENCE_PREFIX)
    // Events of a server that is not shown anymore (they can arrive just after unsubscribing)
    if (channel.startsWith(SERVER_PRESENCE_PREFIX)) {
      if (!isCurrentServer(channel.slice(SERVER_PRESENCE_PREFIX.length))) return
    } else if (!isFriendChannel) {
      return
    }

    const data = parsePayload(message.payload)
    if (!data) return
    const userId = field(data, 'userId')
    const status = parseStatus(field(data, 'status'))
    if (typeof userId !== 'string' || !userId || status === null) return

    const id = normalizeId(userId)
    if (isFriendChannel) friends.add(id)
    setStatus(id, status)

    if (!isFriendChannel) {
      notifyServer({ type: 'presence', serverId: currentServerId.value!, userId: id, status })
    }
  }

  // Only the online users are listed. The rest are already offline here: everything is cleared
  // when the connection is lost, and the members of a server when leaving it
  function handleSnapshot(message: WebSocketMessage) {
    const channel = message.channel ?? ''
    const isFriends = channel === FRIENDS_SNAPSHOT_CHANNEL
    // The snapshot of a server that is not shown anymore
    if (!isFriends && !isCurrentServer(channel)) return

    const data = parsePayload(message.payload)
    const onlineIds = data ? field(data, 'onlineUserIds') : null
    if (!Array.isArray(onlineIds)) return

    const ids: string[] = []
    for (const userId of onlineIds) {
      if (typeof userId !== 'string' || !userId) continue
      const id = normalizeId(userId)
      if (isFriends) friends.add(id)
      setStatus(id, PresenceStatus.Online)
      ids.push(id)
    }

    if (!isFriends) {
      notifyServer({ type: 'snapshot', serverId: currentServerId.value!, onlineUserIds: ids })
    }
  }

  // Receives the Presence and PresenceSnapshot messages of the websocket
  function handleMessage(message: WebSocketMessage) {
    if (message.type === WSMessageType.Presence) handlePresence(message)
    else if (message.type === WSMessageType.PresenceSnapshot) handleSnapshot(message)
  }

  /**
   * Changes the server whose members are followed (undefined: none).
   * The members that are not friends are forgotten: their events stop arriving, so their
   * status would get old. Returns false when it is already the current server
   */
  function setCurrentServer(serverId: string | undefined): boolean {
    const next = serverId ? normalizeId(serverId) : undefined
    if (next === currentServerId.value) return false

    if (currentServerId.value) {
      for (const userId of Object.keys(statuses.value)) {
        if (!friends.has(userId)) delete statuses.value[userId]
      }
    }
    currentServerId.value = next
    return true
  }

  // The connection was lost: nothing is known until the snapshots of the next connection
  function clear() {
    statuses.value = {}
    friends = new Set()
  }

  return {
    statuses,
    currentServerId,
    isOnline,
    handleMessage,
    onServerPresence,
    setCurrentServer,
    clear,
  }
})
