<script setup lang="ts">
  import ChatSideBar from './ChatSideBar.vue'
  import { onMounted, ref, watch } from 'vue'
  import { userInfoStore } from '@/stores/userInfo.ts'
  import type { UserInfo } from '@/types/userInfo.ts'
  import { get } from '@/services/common/api/ApiService.ts'
  import { useRoute } from 'vue-router'
  import { serverInfoStore } from '@/stores/serverInfo.ts'
  import ChatLeftBar from './ChatLeftBar.vue'
  import ChatConversationView from './ChatConversationView.vue'
  import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService.ts'

  const serverInfo = serverInfoStore()

  const userInfo = userInfoStore()
  const route = useRoute()

  onMounted(async () => {
    const user = await get<UserInfo>('users/me')
    userInfo.setUser(user)

    await wsMessageService.tryReconnect()
  })

  const mobileClass = ref('view-channels')

  watch(
    () => route.params.serverId,
    async (newId, oldId) => {
      if (serverInfo.servers.length == 0) {
        await serverInfo.getChatServers()
      }

      if (newId) {
        await serverInfo.setCurrentServer(newId as string)
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
  <section class="chat-layout" :class="mobileClass">
    <ChatSideBar class="chat-side-bar" :class="mobileClass" />
    <ChatLeftBar class="background-soft chat-left-bar" :class="mobileClass" />
    <ChatConversationView class="chat-conversation" :class="mobileClass" />
  </section>
</template>

<style>
  .chat-layout {
    display: grid;
    grid-template-columns: 100px 300px 1fr;
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  .chat-left-bar {
    max-height: calc(100vh - var(--navbar-height));
    display: grid;
    grid-template-rows: auto auto 1fr;
    border-radius: 30px 0 0 30px;
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
