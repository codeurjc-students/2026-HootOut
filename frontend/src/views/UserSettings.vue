<script setup lang="ts">
  import ChatSideBar from '@/components/chat/ChatSideBar.vue'
  import UserSettingsSections from '@/components/user/settings/UserSettingsSections.vue'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'

  const userInfo = userInfoStore()
  const { user } = storeToRefs(userInfo)
</script>

<template>
  <section class="server-settings grid grid-cols-[100px_300px_1fr]">
    <ChatSideBar class="chat-side-bar" />
    <UserSettingsSections
      class="chat-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
    />
    <div class="setting border-1 p-4" v-if="user">
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
