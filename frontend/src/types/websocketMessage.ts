export interface WebSocketMessage {
  id?: string
  replyTo?: string
  type: number
  channel: string | null
  payload?: any | null
}

export enum WSMessageType {
  Invalid = 0,
  Subscribe = 1,
  Unsubscribe = 2,
  ChatMessage = 3,
  Ack = 4,
  Error = 5,
  // Server -> client. A friend or a member of the subscribed server went online or offline.
  // Channel: "presence.user.<userId>" or "presence.server.<serverId>". Payload: PresenceChangedPayload
  Presence = 6,
  // Server -> client. Users online right now. Channel: "friends" (sent after connecting) or the
  // serverId (sent after PresenceSubscribe). Payload: PresenceSnapshotPayload
  PresenceSnapshot = 7,
  // Client -> server. Channel: serverId. Answers with a PresenceSnapshot and then the Ack
  PresenceSubscribe = 8,
  // Client -> server. Channel: serverId
  PresenceUnsubscribe = 9,
}

export enum PresenceStatus {
  Offline = 0,
  Online = 10,
}

export interface PresenceChangedPayload {
  userId: string
  status: PresenceStatus
}

export interface PresenceSnapshotPayload {
  onlineUserIds: string[]
}
