<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import ChannelsList from '../channel/ChannelsList.vue'
  import CreateChannel from '../channel/CreateChannel.vue'

  const serverInfo = serverInfoStore()
  const { currentServer } = storeToRefs(serverInfo) as any
</script>

<template>
  <div class="server-channels-bar">
    <div class="server-name p-4 text-xl font-semibold">
      <div>{{ currentServer?.name }}</div>
    </div>

    <CreateChannel />
    <ChannelsList />
  </div>
</template>

<style>
  .server-name {
    border-bottom: 1px solid rgb(var(--color-background-light));
  }
</style>
