<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo.ts'
  import ChatInfo from './chatConversation/ChatInfo.vue'
  import ChatMessageBar from './chatConversation/ChatMessageBar.vue'
  import ChatMessages from './chatConversation/ChatMessages.vue'
  import { storeToRefs } from 'pinia'

  const { currentChatChannel, currentServer } = storeToRefs(serverInfoStore())
</script>

<template>
  <div
    v-if="currentChatChannel"
    id="chat"
    class="chat-conversation grid grid-rows-[auto_1fr_auto] border-1 background-primary"
  >
    <ChatInfo
      :chat-name="currentChatChannel?.name"
      :back-link="{
        name: 'server',
        params: { serverId: currentServer?.id, channelId: '' },
      }"
    />
    <ChatMessages />
    <ChatMessageBar :chat-name="currentChatChannel?.name" />
  </div>
  <div v-else class="chat-conversation border-1 background-primary p-4">Start a new chat</div>
</template>

<style>
  .chat-conversation {
    max-height: calc(100vh - var(--navbar-height));
  }
</style>
