<script setup lang="ts">
  import ChatConversationView from '@/components/chat/ChatConversationView.vue'
  import ChatSideBar from '@/components/chat/ChatSideBar.vue'
  import FriendsBar from '@/components/directConversation/FriendsBar.vue'
  import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
  import { conversationStore } from '@/stores/conversation'
  import { storeToRefs } from 'pinia'
  import { onMounted, ref, watch } from 'vue'
  import { useRoute } from 'vue-router'

  const route = useRoute()
  const conversation = conversationStore()

  onMounted(async () => {
    await wsMessageService.tryReconnect()
  })

  const mobileClass = ref('view-channels')

  watch(
    () => route.params.chatConversationId,
    async (newId, oldId) => {
      conversation.clearConversation()
      if (newId) {
        conversation.selectConversation(newId as string)
      }
    },
    {
      immediate: true,
    }
  )

  watch(
    () => route.params.chatConversationId,
    async (newId) => {
      if (newId) {
        mobileClass.value = 'view-chat'
      } else {
        mobileClass.value = 'view-channels'
      }
    },
    {
      immediate: true,
    }
  )
</script>

<template>
  <section class="chat-layout grid grid-cols-[100px_300px_1fr]" :class="mobileClass">
    <ChatSideBar class="chat-side-bar" :class="mobileClass" />
    <FriendsBar
      class="chat-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
      :class="mobileClass"
    ></FriendsBar>
    <ChatConversationView
      :class="mobileClass"
      :chat-name="'test'"
      :back-link="{
        name: 'directConversation',
      }"
    />
  </section>
</template>

<style>
  .chat-layout {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  .chat-left-bar {
    max-height: calc(100vh - var(--navbar-height));
  }

  @media (max-width: 520px) {
    .chat-layout.view-chat {
      grid-template-columns: 1fr;
    }

    .chat-layout.view-channels {
      grid-template-columns: 80px 1fr;
    }

    .chat-side-bar.view-chat {
      display: none;
    }

    .chat-left-bar.view-chat {
      display: none;
    }

    .chat-conversation.view-channels {
      display: none;
    }
  }
</style>
