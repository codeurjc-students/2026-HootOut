<script setup lang="ts">
  import { useRoute } from 'vue-router'

  const route = useRoute()
</script>

<template>
  <div>
    <div>User Settings</div>
    <div class="flex flex-col">
      <RouterLink
        :to="{ name: 'userSettingsProfile' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.path.includes('profile') ? 'background-light' : ''"
        >Profile</RouterLink
      >
      <RouterLink
        :to="{ name: 'userSettings' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.path.includes('invitations') ? 'background-light' : ''"
        >Server Invitations</RouterLink
      >
    </div>
  </div>
</template>
