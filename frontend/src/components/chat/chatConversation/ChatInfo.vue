<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'

  const { currentChatChannel, currentServer } = storeToRefs(serverInfoStore())
</script>

<template>
  <div class="chat-info flex background-light px-4 py-2">
    <GoBackLink
      :to="{
        name: 'server',
        params: { serverId: currentServer?.id, channelId: '' },
      }"
    >
      <div>
        {{ currentChatChannel!.name }}
      </div>
    </GoBackLink>
  </div>
</template>
