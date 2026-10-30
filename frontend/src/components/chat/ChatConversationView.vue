<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo.ts'
  import ChatInfo from './chatConversation/ChatInfo.vue'
  import ChatMessageBar from './chatConversation/ChatMessageBar.vue'
  import ChatMessages from './chatConversation/ChatMessages.vue'
  import { storeToRefs } from 'pinia'

  const { currentChatChannel } = storeToRefs(serverInfoStore())
</script>

<template>
  <div
    v-if="currentChatChannel"
    id="chat"
    class="background-primary chat-conversation grid grid-rows-[auto_1fr_auto]"
  >
    <ChatInfo />
    <ChatMessages />
    <ChatMessageBar />
  </div>
  <div v-else id="empty-chat" class="background-primary p-4">Start a new chat</div>
</template>

<style>
  .chat-conversation {
    border: 1px solid rgb(var(--color-border));
    max-height: calc(100vh - var(--navbar-height));
  }

  #empty-chat {
    border: 1px solid rgb(var(--color-border));
  }
</style>
