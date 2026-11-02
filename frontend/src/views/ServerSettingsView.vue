<script setup lang="ts">
  import ChatSideBar from '@/components/chat/ChatSideBar.vue'
  import ServerSettingsSections from '@/components/server/settings/ServerSettingsSections.vue'
  import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import { onMounted, ref, watch } from 'vue'
  import { useRoute } from 'vue-router'

  const serverInfo = serverInfoStore()
  const { currentServer } = storeToRefs(serverInfo)
  const route = useRoute()

  onMounted(async () => {
    await wsMessageService.tryReconnect()
  })

  const mobileClass = ref('view-channels')

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
  <section class="server-settings grid grid-cols-[100px_300px_1fr]" :class="mobileClass">
    <ChatSideBar class="chat-side-bar" :class="mobileClass" />
    <ServerSettingsSections
      class="chat-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
      :class="mobileClass"
    />
    <div class="setting" v-if="currentServer">
      <router-view />
    </div>
  </section>
</template>

<style>
  .server-settings {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }
</style>
