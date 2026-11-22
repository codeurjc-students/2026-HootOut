<script setup lang="ts">
  import ChatInfo from './chatConversation/ChatInfo.vue'
  import ChatMessageBar from './chatConversation/ChatMessageBar.vue'
  import ChatMessages from './chatConversation/ChatMessages.vue'
  import type { RouteLocationRaw } from 'vue-router'

  const { backLink, chatName } = defineProps<{
    backLink: RouteLocationRaw
    chatName: string | undefined
  }>()
</script>

<template>
  <div
    v-if="chatName"
    id="chat"
    class="chat-conversation grid grid-rows-[auto_1fr_auto] border-1 background-primary"
  >
    <ChatInfo :chat-name="chatName" :back-link="backLink" />
    <ChatMessages />
    <ChatMessageBar :chat-name="chatName" />
  </div>
  <div v-else class="chat-conversation border-1 background-primary p-4">Start a new chat</div>
</template>

<style>
  .chat-conversation {
    max-height: calc(100vh - var(--navbar-height));
  }
</style>
