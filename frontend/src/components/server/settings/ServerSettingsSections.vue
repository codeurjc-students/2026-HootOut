<script setup lang="ts">
  import { useRoute } from 'vue-router'
  import ServerName from '../ServerName.vue'

  const route = useRoute()
</script>

<template>
  <div>
    <ServerName />
    <div class="flex flex-col">
      <RouterLink
        :to="{ name: 'serverProfile' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.path.includes('profile') ? 'background-light' : ''"
        >Server Profile</RouterLink
      >
      <RouterLink
        :to="{ name: 'serverMembers' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.path.includes('members') ? 'background-light' : ''"
        >Server Members</RouterLink
      >
    </div>
  </div>
</template>
