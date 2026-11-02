<script setup lang="ts">
  import ChatSideBar from './ChatSideBar.vue'
  import { onMounted, ref, watch } from 'vue'
  import { useRoute } from 'vue-router'
  import { serverInfoStore } from '@/stores/serverInfo.ts'
  import ChatLeftBar from './ChatLeftBar.vue'
  import ChatConversationView from './ChatConversationView.vue'
  import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService.ts'

  const serverInfo = serverInfoStore()

  const route = useRoute()

  onMounted(async () => {
    await wsMessageService.tryReconnect()
  })

  const mobileClass = ref('view-channels')

  watch(
    () => route.params.serverId,
    async (newId, oldId) => {
      if (newId) {
        await serverInfo.setCurrentServer(newId as string)
      } else {
        serverInfo.setCurrentServer('')
      }

      if (route.params.channelId) {
        await serverInfo.selectChannel(route.params.channelId as string)
      }
    },
    {
      immediate: true,
    }
  )

  watch(
    () => route.params.channelId,
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
    <ChatLeftBar
      class="chat-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
      :class="mobileClass"
    />
    <ChatConversationView :class="mobileClass" />
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
