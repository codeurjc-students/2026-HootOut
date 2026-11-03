<script setup lang="ts">
  import { RouterLink, useRoute } from 'vue-router'

  const route = useRoute()
</script>

<template>
  <div class="grid h-full grid-cols-[200px_1fr] border-1">
    <div class="border-left-1 flex w-full flex-col">
      <h2 class="border-bottom-1 mt-[1px] p-4 text-xl font-semibold">Server Members</h2>
      <RouterLink
        :to="{ name: 'serverActiveMembers' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.name == 'serverActiveMembers' ? 'background-light' : ''"
        >Active Members</RouterLink
      >
      <RouterLink
        :to="{ name: 'serverInvitations' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.name == 'serverInvitations' ? 'background-light' : ''"
        >Server Invitations</RouterLink
      >
    </div>
    <div class="h-full w-full background-soft">
      <router-view />
    </div>
  </div>
</template>
