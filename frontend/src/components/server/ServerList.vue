<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { ChatServer } from '@/types'
  import { storeToRefs } from 'pinia'
  import { onMounted } from 'vue'
  import { useRouter } from 'vue-router'

  const router = useRouter()

  const serverInfo = serverInfoStore()
  const { servers, currentServer } = storeToRefs(serverInfo)
</script>

<template>
  <RouterLink
    :to="{ name: 'server', params: { serverId: server.id } }"
    v-for="server in servers"
    class="round-btn pointer mb-2 flex h-full justify-center self-center"
    :class="{ 'background-light': currentServer?.id == server.id }"
    :key="server.id"
    v-tooltip:right="server.name"
    :aria-label="server.name"
    :server-id="server.id"
  >
    <span class="server-btn-text text- inline-block self-center text-center text-[2rem]">
      {{ server.name.charAt(0).toUpperCase() }}
    </span>
  </RouterLink>
</template>
