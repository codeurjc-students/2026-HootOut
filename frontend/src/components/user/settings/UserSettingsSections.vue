<script setup lang="ts">
  import { useRoute } from 'vue-router'

  const route = useRoute()
</script>

<template>
  <div>
    <h1 class="p-4 text-xl font-semibold border-bottom-1">User Settings</h1>
    <div class="flex flex-col">
      <RouterLink
        :to="{ name: 'userSettingsProfile' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Profile</RouterLink
      >
      <RouterLink
        :to="{ name: 'userServerInvitations' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Server Invitations</RouterLink
      >
      <RouterLink
        :to="{ name: 'friendRequests' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Friend Requests</RouterLink
      >
    </div>
  </div>
</template>
