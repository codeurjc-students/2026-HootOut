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
  <div class="chat-info background-light">
    <div class="go-back-btn material-symbols-sharp" @click="goToChannelList">chevron_left</div>
    <div>
      {{ currentChatChannel!.name }}
    </div>
  </div>
</template>

<style>
  .chat-info {
    padding: 0.5rem 1rem;
    display: flex;
  }

  .go-back-btn {
    display: none;
  }

  @media (max-width: 520px) {
    .go-back-btn {
      display: inline-block;
      padding-right: 0.5rem;
    }
  }
</style>
