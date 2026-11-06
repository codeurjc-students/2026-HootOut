<script setup lang="ts">
  import ChatSideBar from '@/components/chat/ChatSideBar.vue'
  import UserSettingsSections from '@/components/user/settings/UserSettingsSections.vue'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'
  import { ref, watch } from 'vue'
  import { useRoute } from 'vue-router'

  const route = useRoute()
  const userInfo = userInfoStore()
  const { user } = storeToRefs(userInfo)

  const mobileClass = ref('settings-list')

  watch(
    () => route.name,
    async (name) => {
      if (name == 'userSettings') {
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
  <section class="user-settings grid grid-cols-[100px_300px_1fr]" :class="mobileClass">
    <ChatSideBar class="settings-side-bar" :class="mobileClass" />
    <UserSettingsSections
      class="settings-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
      :class="mobileClass"
    />
    <div class="settings border-1 p-2 md:p-4" v-if="user" :class="mobileClass">
      <router-view />
    </div>
  </section>
</template>

<style>
  .user-settings {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  @media (max-width: 520px) {
    .user-settings.settings-view {
      grid-template-columns: 1fr;
    }

    .user-settings.settings-list {
      grid-template-columns: 80px 1fr;
    }

    .settings-side-bar.settings-view {
      display: none;
    }
    .settings-left-bar.settings-view {
      display: none;
    }

    .settings.settings-list {
      display: none;
    }
  }
</style>
