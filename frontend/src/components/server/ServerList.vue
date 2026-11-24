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
      <div class="w-full flex justify-center hover:background-soft">
        <RouterLink
          :to="{ name: 'server', params: { serverId: (item as any).id } }"
          class="round-btn pointer mb-2 flex h-full justify-center self-center relative"
          active-class="background-light active-server"
          :key="(item as any).id"
          v-tooltip:right="(item as any).name"
          :aria-label="(item as any).name"
          :server-id="(item as any).id"
        >
        <img v-if="(item as any).profilePicture" :src="(item as any).profilePicture.url" 
        class="rounded-full object-cover w-full relative"> 
        </img>
          <span
            v-else
            class="server-btn-text inline-block self-center text-center text-[2rem]"
          >
            {{ item.name.charAt(0).toUpperCase() }}
          </span>
        </RouterLink>
      </div>
    </template>
  </InfiniteScrollList>
</template>

<style>
.active-server::before {
  content: '';
  position: absolute;
  background-color: rgb(var(--color-text));
  left: -.85rem;
  top: 50%;
  width: .4rem;
  height: 2rem;
  transform: translateY(-50%);
  z-index: 10;
  border-radius: 4px;
  
   animation: zoomInAnimation 0.2s ease 1 normal forwards;
}

@keyframes zoomInAnimation {
  0% { height: 0; opacity: 0; }
  100% {  height: 2rem; opacity: 1; }
}

</style>
