<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import { computed, onMounted } from 'vue'
  import InfiniteScrollList from '../shared/infiniteScroll/InfiniteScrollList.vue'

  const serverInfo = serverInfoStore()
  const { servers, currentServer } = storeToRefs(serverInfo)

  onMounted(async () => {
    servers.value = []
  })

  async function getServerList(next: string | null) {
    const page = await serverInfo.getChatServers(next)
    return page?.next
  }

  const serverList = computed(() => {
    const result = []
    if (
      currentServer?.value &&
      servers.value?.filter((x) => x.id == currentServer?.value?.id).length == 0
    ) {
      result.push(currentServer.value)
    }
    result.push(...servers.value)
    return result
  })
</script>

<template>
  <InfiniteScrollList :items="serverList" :load-more="getServerList" class="flex flex-col">
    <template v-slot:scroll-item="{ item }">
      <RouterLink
        :to="{ name: 'server', params: { serverId: (item as any).id } }"
        class="round-btn pointer mb-2 flex h-full justify-center self-center"
        :class="{ 'background-light': currentServer?.id == (item as any).id }"
        :key="(item as any).id"
        v-tooltip:right="(item as any).name"
        :aria-label="(item as any).name"
        :server-id="(item as any).id"
      >
        <span class="server-btn-text text- inline-block self-center text-center text-[2rem]">
          {{ item.name.charAt(0).toUpperCase() }}
        </span>
      </RouterLink>
    </template>
  </InfiniteScrollList>
</template>
