<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import { useRouter } from 'vue-router'

  const router = useRouter()

  const { currentChatChannel, currentServer } = storeToRefs(serverInfoStore())

  async function goToChannelList() {
    router.push({
      name: 'server',
      params: { serverId: currentServer.value?.id, channelId: '' },
    })
  }
</script>

<template>
  <div class="chat-info background-light flex px-4 py-2">
    <div
      class="go-back-btn material-symbols-sharp inline-block pr-2 md:hidden!"
      @click="goToChannelList"
    >
      chevron_left
    </div>
    <div>
      {{ currentChatChannel!.name }}
    </div>
  </div>
</template>
