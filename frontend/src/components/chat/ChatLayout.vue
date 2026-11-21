<script setup lang="ts">
  import ChatSideBar from './ChatSideBar.vue'
  import { computed, onMounted, watch } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { serverInfoStore } from '@/stores/serverInfo'
  import ChatLeftBar from './ChatLeftBar.vue'
  import ChatConversationView from './ChatConversationView.vue'
  import wsMessageService from '@/services/common/websocketsMessage/WebSocketsMessageService'
  import { storeToRefs } from 'pinia'

  const serverInfo = serverInfoStore()
  const { currentChatChannel, currentServer } = storeToRefs(serverInfo)

  const route = useRoute()
  const router = useRouter()

  onMounted(async () => {
    await wsMessageService.tryReconnect()
  })

  // On mobile only one side is shown: the channels, or the chat when a channel is open
  const mobileClass = computed(() => (route.params.channelId ? 'view-chat' : 'view-channels'))

  // The app remounts this component on every route change (RouterView key), so this runs
  // on mount with the server and the channel of the route
  watch(
    () => route.params.serverId,
    async (newId) => {
      if (newId) {
        try {
          await serverInfo.setCurrentServer(newId as string)
        } catch (err) {
          // The server does not exist or the user cannot open it
          await router.push({ name: 'chat' })
          return
        }
      }

      if (route.params.channelId) {
        try {
          await serverInfo.selectChannel(route.params.channelId as string)
        } catch (err) {
          // TODO: show the error to the user (the messages could not be loaded)
        }
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
    <ChatConversationView
      :class="mobileClass"
      :chat-name="currentChatChannel?.name"
      :back-link="{
        name: 'server',
        params: { serverId: currentServer?.id },
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

  @media (width < 520px) {
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
