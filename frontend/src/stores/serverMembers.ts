import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import ChatServerService from '@/services/chatServer/ChatServerService'
import { PresenceStatus, type PageRequest, type ServerMember } from '@/types'
import type { ServerPresenceEvent } from '@/stores/presence'

// Offline members loaded per page. The online ones come in a single request (capped by the backend)
export const OFFLINE_PAGE_SIZE = 50

function normalizeId(id: string): string {
  return id.toLowerCase()
}

// Same order as the backend ("username" ignoring case and accents)
const collator = new Intl.Collator(undefined, { sensitivity: 'base' })

function byUsername(a: ServerMember, b: ServerMember): number {
  return collator.compare(a.username, b.username) || a.userId.localeCompare(b.userId)
}

/**
 * Online and offline members of the server shown in the chat.
 *
 * - The usernames come from the API: the online members in one request (`load`) and the
 *   offline ones in pages sorted by username (`loadOfflinePage`, called by InfiniteScrollList).
 * - Who is online comes from the presence of the websocket (snapshot of the server and its
 *   events), through `handlePresenceEvent`. Until the snapshot arrives the answer of the API is
 *   used. A member that changes status moves from one list to the other without asking the API.
 * - A member that goes online and was not loaded yet (an offline member of a later page) is
 *   unknown here: the online list is requested again to get its username.
 */
export const serverMembersStore = defineStore('serverMembers', () => {
  const serverId = ref<string>()

  // userId -> member: every member loaded, online or offline
  const members = ref<Record<string, ServerMember>>({})
  // Online members of the server. Starts with the API and then follows the websocket
  const onlineIds = ref<Set<string>>(new Set())
  const snapshotReceived = ref(false)

  // Counts of the API at load time. The online ones are only used until the snapshot arrives
  const apiOnlineTotal = ref(0)
  const apiOnlineItems = ref(0)
  // Members of the server, from the total of the last offline page plus the online ones then.
  // Unknown (null) until the first offline page arrives
  const memberTotal = ref<number | null>(null)
  // The backend caps the online list: when true not every online member has a username here
  const onlineTruncated = ref(false)

  // Every offline page loaded (the last one had no cursor), and the last member loaded
  const offlineComplete = ref(false)
  const lastOfflineLoaded = ref<ServerMember>()

  const loading = ref(false)
  const error = ref(false)
  const offlineError = ref(false)
  // Changes on every load and retry: the view uses it as the key of InfiniteScrollList, so the
  // list starts again from the first page
  const offlineListVersion = ref(0)

  // Each load invalidates the answers that belong to a previous one
  let generation = 0
  // Events received while the first requests are running: applied when they finish
  let pendingEvents: ServerPresenceEvent[] | null = null

  // Total of the last offline page, until the online members arrive
  let lastOfflinePageTotal: number | null = null

  // Refresh of the online list in progress, and whether it has to run once more after it
  let refreshing: Promise<void> | null = null
  let refreshAgain = false

  // After the snapshot every online member is in onlineIds. Before it, the API may have capped
  // the list: its total plus the changes of the events since then
  const onlineCount = computed(() =>
    snapshotReceived.value
      ? onlineIds.value.size
      : Math.max(0, apiOnlineTotal.value - apiOnlineItems.value + onlineIds.value.size)
  )
  const offlineCount = computed(() =>
    memberTotal.value === null ? null : Math.max(0, memberTotal.value - onlineCount.value)
  )

  const onlineMembers = computed(() =>
    [...onlineIds.value]
      .map((id) => members.value[id])
      .filter((m): m is ServerMember => !!m)
      .sort(byUsername)
  )

  // Online members without username here (only when the backend capped the list)
  const hiddenOnlineCount = computed(() =>
    Math.max(0, onlineCount.value - onlineMembers.value.length)
  )

  // A member that went offline but sorts after the last page loaded is not shown yet: it comes
  // in its page, so the list never shows a gap in the order
  const offlineMembers = computed(() => {
    const last = lastOfflineLoaded.value
    return Object.values(members.value)
      .filter((m) => !onlineIds.value.has(normalizeId(m.userId)))
      .filter((m) => offlineComplete.value || (!!last && byUsername(m, last) <= 0))
      .sort(byUsername)
  })

  function addMembers(items: ServerMember[]) {
    for (const member of items) {
      if (!member?.userId) continue
      members.value[normalizeId(member.userId)] = member
    }
  }

  function offlineRequest(next: string | null): PageRequest {
    return { next, pageSize: OFFLINE_PAGE_SIZE, sortBy: 'username', descending: false }
  }

  function reset(id: string | undefined) {
    generation++
    serverId.value = id ? normalizeId(id) : undefined
    members.value = {}
    onlineIds.value = new Set()
    snapshotReceived.value = false
    apiOnlineTotal.value = 0
    apiOnlineItems.value = 0
    memberTotal.value = null
    onlineTruncated.value = false
    offlineComplete.value = false
    lastOfflineLoaded.value = undefined
    loading.value = false
    error.value = false
    offlineError.value = false
    offlineListVersion.value++
    pendingEvents = null
    lastOfflinePageTotal = null
    refreshing = null
    refreshAgain = false
  }

  /**
   * Loads the online members of the server. The offline ones are loaded by InfiniteScrollList
   * with `loadOfflinePage`. undefined: forget the current one
   */
  async function load(id: string | undefined): Promise<void> {
    reset(id)
    if (!id) return

    const current = generation
    loading.value = true
    pendingEvents = []

    try {
      const online = await ChatServerService.getOnlineServerMembers(id)
      if (current !== generation) return

      addMembers(online.items)
      // A snapshot received meanwhile is in pendingEvents and replaces this below
      onlineIds.value = new Set(online.items.map((m) => normalizeId(m.userId)))
      apiOnlineTotal.value = online.total
      apiOnlineItems.value = onlineIds.value.size
      onlineTruncated.value = online.total > online.items.length
      if (lastOfflinePageTotal !== null) memberTotal.value = lastOfflinePageTotal + online.total
    } catch {
      if (current !== generation) return
      error.value = true
    } finally {
      if (current === generation) {
        loading.value = false
        const events = pendingEvents ?? []
        pendingEvents = null
        // Without the members the events are useless: the next load brings the current state
        if (!error.value) events.forEach(applyEvent)
      }
    }
  }

  /**
   * A page of offline members (next = null: the first one). Returns the cursor of the next page,
   * null when there are no more: the loadMore of InfiniteScrollList
   */
  async function loadOfflinePage(next: string | null): Promise<string | null> {
    const id = serverId.value
    if (!id) return null

    const current = generation
    try {
      const page = await ChatServerService.getOfflineServerMembers(id, offlineRequest(next))
      // A page of a previous server: the list of that server is gone
      if (current !== generation) return null

      addMembers(page.items)
      lastOfflineLoaded.value = page.items.at(-1) ?? lastOfflineLoaded.value
      offlineComplete.value = !page.next
      // The total excludes the members online when the page was read. If the online members are
      // still loading, load() adds them when they arrive
      lastOfflinePageTotal = page.total
      if (!loading.value) memberTotal.value = page.total + onlineCount.value
      return page.next ?? null
    } catch {
      if (current === generation) offlineError.value = true
      // Stops the list: the view shows the error with a button to try again
      return null
    }
  }

  /** Loads the offline members again from the first page (after an error) */
  function retryOffline() {
    offlineError.value = false
    offlineComplete.value = false
    lastOfflineLoaded.value = undefined
    offlineListVersion.value++
  }

  // Asks the API again for the online members, to get the usernames that are missing
  function refreshOnline(): Promise<void> {
    if (refreshing) {
      refreshAgain = true
      return refreshing
    }

    const id = serverId.value!
    const current = generation
    refreshing = (async () => {
      try {
        const online = await ChatServerService.getOnlineServerMembers(id)
        if (current !== generation) return
        // Only the usernames: who is online is decided by the websocket
        addMembers(online.items)
        onlineTruncated.value = online.total > online.items.length
      } catch {
        // The member is counted but not listed until the next refresh
      } finally {
        if (current === generation) {
          refreshing = null
          if (refreshAgain) {
            refreshAgain = false
            void refreshOnline()
          }
        }
      }
    })()
    return refreshing
  }

  function needsUsername(userId: string): boolean {
    return !members.value[userId] && !onlineTruncated.value
  }

  function applyEvent(event: ServerPresenceEvent) {
    if (event.type === 'snapshot') {
      snapshotReceived.value = true
      onlineIds.value = new Set(event.onlineUserIds)
      if (event.onlineUserIds.some(needsUsername)) void refreshOnline()
      return
    }

    const next = new Set(onlineIds.value)
    if (event.status === PresenceStatus.Online) {
      // Repeated "online" (reconnection inside the grace period): nothing changes
      if (next.has(event.userId)) return
      next.add(event.userId)
      onlineIds.value = next
      if (needsUsername(event.userId)) void refreshOnline()
    } else {
      if (!next.delete(event.userId)) return
      onlineIds.value = next
    }
  }

  /** Receives the presence of the members of the current server (see presenceStore.onServerPresence) */
  function handlePresenceEvent(event: ServerPresenceEvent) {
    if (!serverId.value || normalizeId(event.serverId) !== serverId.value) return
    if (pendingEvents) {
      pendingEvents.push(event)
      return
    }
    // The load failed: there is nothing to update
    if (error.value) return
    applyEvent(event)
  }

  return {
    serverId,
    loading,
    error,
    offlineError,
    offlineListVersion,
    onlineMembers,
    offlineMembers,
    onlineCount,
    offlineCount,
    hiddenOnlineCount,
    load,
    loadOfflinePage,
    retryOffline,
    handlePresenceEvent,
  }
})
