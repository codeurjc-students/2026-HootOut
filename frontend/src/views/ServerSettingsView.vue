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

  const mobileClass = ref('settings-list')

  watch(
    () => route.name,
    async (name) => {
      if (name == 'serverSettings') {
        mobileClass.value = 'settings-list'
      } else {
        mobileClass.value = 'settings-view'
      }
    },
    {
      immediate: true,
    }
  )
</script>

<template>
  <section class="server-settings grid grid-cols-[100px_300px_1fr]" :class="mobileClass">
    <ChatSideBar class="settings-side-bar" :class="mobileClass" />
    <ServerSettingsSections
      class="settings-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
      :class="mobileClass"
    />
    <div class="settings-view" v-if="currentServer" :class="mobileClass">
      <router-view />
    </div>
  </section>
</template>

<style>
  .server-settings {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  @media (max-width: 520px) {
    .server-settings.settings-view {
      grid-template-columns: 1fr;
    }

    .server-settings.settings-list {
      grid-template-columns: 80px 1fr;
    }

    .settings-side-bar.settings-view {
      display: none;
    }

    .settings-left-bar.settings-view {
      display: none;
    }

    .settings-view.settings-list {
      display: none;
    }
  }
</style>
