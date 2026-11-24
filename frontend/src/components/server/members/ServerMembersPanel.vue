<script setup lang="ts">
  import { onBeforeUnmount, watch } from 'vue'
  import { storeToRefs } from 'pinia'
  import InfiniteScrollList from '@/components/shared/infiniteScroll/InfiniteScrollList.vue'
  import PresenceIndicator from '@/components/shared/presence/PresenceIndicator.vue'
  import { presenceStore } from '@/stores/presence'
  import { serverMembersStore } from '@/stores/serverMembers'
  import type { ServerMember } from '@/types'

  const { serverId } = defineProps<{ serverId?: string }>()

  const store = serverMembersStore()
  const {
    loading,
    error,
    offlineError,
    offlineListVersion,
    onlineMembers,
    offlineMembers,
    onlineCount,
    offlineCount,
    hiddenOnlineCount,
  } = storeToRefs(store)

  // The presence of the members (who is online) comes from the websocket. PresenceSync
  // subscribes to the server of the route, this only listens
  const stopListening = presenceStore().onServerPresence(store.handlePresenceEvent)

  watch(
    () => serverId,
    (id) => store.load(id),
    { immediate: true }
  )

  onBeforeUnmount(stopListening)

  function retry() {
    void store.load(serverId)
  }

  const asMember = (item: unknown) => item as ServerMember
</script>

<template>
  <!-- No display utility on the root: the layout decides when the panel is shown -->
  <aside
    id="server-members"
    class="server-members overflow-y-auto background-soft p-3"
    aria-label="Server members"
  >
    <p v-if="loading" class="text-sm opacity-70" role="status">Loading members…</p>

    <div v-else-if="error" class="text-sm" role="alert">
      <p>Could not load the members.</p>
      <button type="button" class="mt-2 underline" @click="retry">Retry</button>
    </div>

    <div v-else class="flex flex-col gap-4">
      <section aria-labelledby="online-members-title">
        <h3 id="online-members-title" class="mb-2 text-xs font-semibold uppercase opacity-70">
          Online — {{ onlineCount }}
        </h3>
        <ul id="online-members" class="flex flex-col gap-1">
          <li
            v-for="member in onlineMembers"
            :key="member.userId"
            class="member flex items-center gap-2 truncate"
            data-member-status="online"
          >
            <PresenceIndicator :user-id="member.userId" />
            <span class="truncate">{{ member.username }}</span>
          </li>
        </ul>
        <p v-if="hiddenOnlineCount > 0" class="mt-1 text-xs opacity-70">
          and {{ hiddenOnlineCount }} more
        </p>
      </section>

      <section id="offline-members" aria-labelledby="offline-members-title">
        <h3 id="offline-members-title" class="mb-2 text-xs font-semibold uppercase opacity-70">
          Offline<template v-if="offlineCount !== null"> — {{ offlineCount }}</template>
        </h3>
        <!-- The key starts the list again from the first page (new server or retry) -->
        <InfiniteScrollList
          :key="offlineListVersion"
          :items="offlineMembers"
          :load-more="store.loadOfflinePage"
          class="flex flex-col gap-1 opacity-70"
        >
          <template v-slot:scroll-item="{ item }">
            <div class="member flex items-center gap-2 truncate" data-member-status="offline">
              <PresenceIndicator :user-id="asMember(item).userId" />
              <span class="truncate">{{ asMember(item).username }}</span>
            </div>
          </template>
        </InfiniteScrollList>
        <div v-if="offlineError" class="mt-2 text-xs" role="alert">
          <p>Could not load the offline members.</p>
          <button type="button" class="underline" @click="store.retryOffline()">Retry</button>
        </div>
      </section>
    </div>
  </aside>
</template>

<style scoped>
  .server-members {
    max-height: calc(100vh - var(--navbar-height));
  }
</style>
