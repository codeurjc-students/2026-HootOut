import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { presenceStore } from '@/stores/presence'
import { PresenceStatus, WSMessageType, type WebSocketMessage } from '@/types'

// ---------- helpers ----------

const FRIEND = 'aaaaaaaa-0000-0000-0000-000000000001'
const OTHER_FRIEND = 'aaaaaaaa-0000-0000-0000-000000000002'
const MEMBER = 'bbbbbbbb-0000-0000-0000-000000000001'
const SERVER = 'cccccccc-0000-0000-0000-000000000001'
const OTHER_SERVER = 'cccccccc-0000-0000-0000-000000000002'

function presence(
  channel: string,
  userId: string,
  status: PresenceStatus | string
): WebSocketMessage {
  return {
    type: WSMessageType.Presence,
    channel,
    payload: JSON.stringify({ UserId: userId, Status: status }),
  }
}

const friendPresence = (userId: string, status: PresenceStatus | string) =>
  presence(`presence.user.${userId}`, userId, status)

const serverPresence = (serverId: string, userId: string, status: PresenceStatus) =>
  presence(`presence.server.${serverId}`, userId, status)

function snapshot(channel: string, onlineUserIds: unknown): WebSocketMessage {
  return {
    type: WSMessageType.PresenceSnapshot,
    channel,
    payload: JSON.stringify({ OnlineUserIds: onlineUserIds }),
  }
}

// ---------- tests ----------

describe('presence store', () => {
  let store: ReturnType<typeof presenceStore>

  beforeEach(() => {
    setActivePinia(createPinia())
    store = presenceStore()
  })

  describe('isOnline', () => {
    it('is false for unknown users', () => {
      expect(store.isOnline(FRIEND)).toBe(false)
    })

    it('is false without a user id', () => {
      expect(store.isOnline(undefined)).toBe(false)
      expect(store.isOnline(null)).toBe(false)
      expect(store.isOnline('')).toBe(false)
    })

    it('does not depend on the case of the id', () => {
      store.handleMessage(friendPresence(FRIEND.toUpperCase(), PresenceStatus.Online))

      expect(store.isOnline(FRIEND)).toBe(true)
      expect(store.isOnline(FRIEND.toUpperCase())).toBe(true)
    })
  })

  describe('snapshot of the friends', () => {
    it('marks the listed friends online', () => {
      store.handleMessage(snapshot('friends', [FRIEND, OTHER_FRIEND]))

      expect(store.isOnline(FRIEND)).toBe(true)
      expect(store.isOnline(OTHER_FRIEND)).toBe(true)
      expect(store.isOnline(MEMBER)).toBe(false)
    })

    it('accepts the fields in camelCase', () => {
      store.handleMessage({
        type: WSMessageType.PresenceSnapshot,
        channel: 'friends',
        payload: JSON.stringify({ onlineUserIds: [FRIEND] }),
      })

      expect(store.isOnline(FRIEND)).toBe(true)
    })

    it('accepts an empty list', () => {
      store.handleMessage(snapshot('friends', []))

      expect(store.statuses).toEqual({})
    })

    it.each([
      ['no payload', { type: WSMessageType.PresenceSnapshot, channel: 'friends' }],
      ['invalid JSON', { type: WSMessageType.PresenceSnapshot, channel: 'friends', payload: '{' }],
      ['no list', { type: WSMessageType.PresenceSnapshot, channel: 'friends', payload: '{}' }],
    ])('ignores a snapshot with %s', (_name, message) => {
      expect(() => store.handleMessage(message as WebSocketMessage)).not.toThrow()
      expect(store.statuses).toEqual({})
    })

    it('ignores the entries of the list that are not ids', () => {
      store.handleMessage(snapshot('friends', [FRIEND, 3, null, '']))

      expect(Object.keys(store.statuses)).toEqual([FRIEND])
    })
  })

  describe('presence of a friend', () => {
    it('marks the friend online and offline', () => {
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Online))
      expect(store.isOnline(FRIEND)).toBe(true)

      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Offline))
      expect(store.isOnline(FRIEND)).toBe(false)
      // Offline users are not kept
      expect(store.statuses).toEqual({})
    })

    it('treats a repeated online as a no-op', () => {
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Online))
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Online))

      expect(store.statuses).toEqual({ [FRIEND]: PresenceStatus.Online })
    })

    it('treats an offline of a user that is not online as a no-op', () => {
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Offline))

      expect(store.statuses).toEqual({})
    })

    it('accepts the status by name', () => {
      store.handleMessage(friendPresence(FRIEND, 'Online'))
      expect(store.isOnline(FRIEND)).toBe(true)

      store.handleMessage(friendPresence(FRIEND, 'Offline'))
      expect(store.isOnline(FRIEND)).toBe(false)
    })

    it('accepts the fields in camelCase', () => {
      store.handleMessage({
        type: WSMessageType.Presence,
        channel: `presence.user.${FRIEND}`,
        payload: JSON.stringify({ userId: FRIEND, status: PresenceStatus.Online }),
      })

      expect(store.isOnline(FRIEND)).toBe(true)
    })

    it.each([
      ['an unknown status', JSON.stringify({ UserId: FRIEND, Status: 5 })],
      ['no user', JSON.stringify({ Status: PresenceStatus.Online })],
      ['invalid JSON', 'not json'],
      ['no payload', undefined],
    ])('ignores an event with %s', (_name, payload) => {
      store.handleMessage({
        type: WSMessageType.Presence,
        channel: `presence.user.${FRIEND}`,
        payload,
      })

      expect(store.statuses).toEqual({})
    })

    it('ignores an event of an unknown channel', () => {
      store.handleMessage(presence('something-else', FRIEND, PresenceStatus.Online))
      store.handleMessage(presence(null as unknown as string, FRIEND, PresenceStatus.Online))

      expect(store.statuses).toEqual({})
    })
  })

  describe('messages of other types', () => {
    it('are ignored', () => {
      store.handleMessage({
        type: WSMessageType.ChatMessage,
        channel: 'friends',
        payload: JSON.stringify({ OnlineUserIds: [FRIEND] }),
      })

      expect(store.statuses).toEqual({})
    })
  })

  describe('server', () => {
    beforeEach(() => {
      store.setCurrentServer(SERVER)
    })

    it('keeps the current server normalized', () => {
      store.setCurrentServer(OTHER_SERVER.toUpperCase())

      expect(store.currentServerId).toBe(OTHER_SERVER)
    })

    it('returns false when the server does not change', () => {
      expect(store.setCurrentServer(SERVER)).toBe(false)
      expect(store.setCurrentServer(SERVER.toUpperCase())).toBe(false)
      expect(store.setCurrentServer(OTHER_SERVER)).toBe(true)
      expect(store.setCurrentServer(undefined)).toBe(true)
      expect(store.setCurrentServer(undefined)).toBe(false)
    })

    it('marks the members of the snapshot of the current server online', () => {
      store.handleMessage(snapshot(SERVER, [MEMBER]))

      expect(store.isOnline(MEMBER)).toBe(true)
    })

    it('ignores the snapshot of another server', () => {
      store.handleMessage(snapshot(OTHER_SERVER, [MEMBER]))

      expect(store.isOnline(MEMBER)).toBe(false)
    })

    it('updates the members with the events of the current server', () => {
      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))
      expect(store.isOnline(MEMBER)).toBe(true)

      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Offline))
      expect(store.isOnline(MEMBER)).toBe(false)
    })

    it('ignores the events of another server (e.g. just after unsubscribing)', () => {
      store.handleMessage(serverPresence(OTHER_SERVER, MEMBER, PresenceStatus.Online))

      expect(store.isOnline(MEMBER)).toBe(false)
    })

    it('ignores the events of a server when none is shown', () => {
      store.setCurrentServer(undefined)

      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))
      store.handleMessage(snapshot(SERVER, [MEMBER]))

      expect(store.isOnline(MEMBER)).toBe(false)
    })

    it('forgets the members that are not friends when the server changes', () => {
      store.handleMessage(snapshot(SERVER, [MEMBER, FRIEND]))
      store.handleMessage(snapshot('friends', [FRIEND]))

      store.setCurrentServer(OTHER_SERVER)

      expect(store.isOnline(MEMBER)).toBe(false)
      expect(store.isOnline(FRIEND)).toBe(true)
    })

    it('keeps the friends known by their own events when leaving the server', () => {
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Online))
      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))

      store.setCurrentServer(undefined)

      expect(store.statuses).toEqual({ [FRIEND]: PresenceStatus.Online })
    })

    it('keeps the members when the server is selected again', () => {
      store.handleMessage(snapshot(SERVER, [MEMBER]))

      store.setCurrentServer(SERVER)

      expect(store.isOnline(MEMBER)).toBe(true)
    })

    it('a friend that is also a member gets the same status from both channels', () => {
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Online))
      store.handleMessage(serverPresence(SERVER, FRIEND, PresenceStatus.Online))
      expect(store.isOnline(FRIEND)).toBe(true)

      store.handleMessage(serverPresence(SERVER, FRIEND, PresenceStatus.Offline))
      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Offline))
      expect(store.isOnline(FRIEND)).toBe(false)
    })
  })

  describe('clear', () => {
    it('forgets every user and keeps the current server', () => {
      store.setCurrentServer(SERVER)
      store.handleMessage(snapshot('friends', [FRIEND]))
      store.handleMessage(snapshot(SERVER, [MEMBER]))

      store.clear()

      expect(store.statuses).toEqual({})
      expect(store.currentServerId).toBe(SERVER)
    })

    it('forgets which users are friends', () => {
      store.setCurrentServer(SERVER)
      store.handleMessage(snapshot('friends', [FRIEND]))
      store.clear()
      // After reconnecting the friend only arrives in the snapshot of the server
      store.handleMessage(snapshot(SERVER, [FRIEND]))

      store.setCurrentServer(undefined)

      expect(store.isOnline(FRIEND)).toBe(false)
    })
  })

  describe('onServerPresence', () => {
    it('receives the events of the current server with the ids normalized', () => {
      const listener = vi.fn()
      store.onServerPresence(listener)
      store.setCurrentServer(SERVER.toUpperCase())

      store.handleMessage(serverPresence(SERVER, MEMBER.toUpperCase(), PresenceStatus.Online))
      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Offline))

      expect(listener.mock.calls).toEqual([
        [{ type: 'presence', serverId: SERVER, userId: MEMBER, status: PresenceStatus.Online }],
        [{ type: 'presence', serverId: SERVER, userId: MEMBER, status: PresenceStatus.Offline }],
      ])
    })

    it('receives the snapshot of the current server with only the valid ids', () => {
      const listener = vi.fn()
      store.onServerPresence(listener)
      store.setCurrentServer(SERVER)

      store.handleMessage(snapshot(SERVER, [MEMBER.toUpperCase(), 42, '', FRIEND]))

      expect(listener).toHaveBeenCalledExactlyOnceWith({
        type: 'snapshot',
        serverId: SERVER,
        onlineUserIds: [MEMBER, FRIEND],
      })
    })

    it('receives an empty snapshot', () => {
      const listener = vi.fn()
      store.onServerPresence(listener)
      store.setCurrentServer(SERVER)

      store.handleMessage(snapshot(SERVER, []))

      expect(listener).toHaveBeenCalledExactlyOnceWith({
        type: 'snapshot',
        serverId: SERVER,
        onlineUserIds: [],
      })
    })

    it('does not receive the presence or the snapshot of the friends', () => {
      const listener = vi.fn()
      store.onServerPresence(listener)
      store.setCurrentServer(SERVER)

      store.handleMessage(friendPresence(FRIEND, PresenceStatus.Online))
      store.handleMessage(snapshot('friends', [OTHER_FRIEND]))

      expect(listener).not.toHaveBeenCalled()
    })

    it('does not receive the events of another server or without a current server', () => {
      const listener = vi.fn()
      store.onServerPresence(listener)

      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))
      store.setCurrentServer(SERVER)
      store.handleMessage(serverPresence(OTHER_SERVER, MEMBER, PresenceStatus.Online))
      store.handleMessage(snapshot(OTHER_SERVER, [MEMBER]))

      expect(listener).not.toHaveBeenCalled()
    })

    it('does not receive invalid payloads', () => {
      const listener = vi.fn()
      store.onServerPresence(listener)
      store.setCurrentServer(SERVER)

      store.handleMessage({
        type: WSMessageType.Presence,
        channel: `presence.server.${SERVER}`,
        payload: 'not json',
      })
      store.handleMessage({ type: WSMessageType.PresenceSnapshot, channel: SERVER, payload: '{}' })

      expect(listener).not.toHaveBeenCalled()
    })

    it('stops calling a listener after removing it', () => {
      const listener = vi.fn()
      const remove = store.onServerPresence(listener)
      store.setCurrentServer(SERVER)

      remove()
      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))

      expect(listener).not.toHaveBeenCalled()
    })

    it('keeps calling the other listeners and updating the status when one throws', () => {
      const broken = vi.fn(() => {
        throw new Error('broken')
      })
      const listener = vi.fn()
      store.onServerPresence(broken)
      store.onServerPresence(listener)
      store.setCurrentServer(SERVER)

      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))

      expect(listener).toHaveBeenCalledOnce()
      expect(store.isOnline(MEMBER)).toBe(true)
    })

    it('updates the status before calling the listeners', () => {
      let seen: boolean | undefined
      store.onServerPresence(() => (seen = store.isOnline(MEMBER)))
      store.setCurrentServer(SERVER)

      store.handleMessage(serverPresence(SERVER, MEMBER, PresenceStatus.Online))

      expect(seen).toBe(true)
    })
  })
})
