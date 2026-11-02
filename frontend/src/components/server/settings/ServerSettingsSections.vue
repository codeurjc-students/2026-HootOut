<script setup lang="ts">
  import ServerName from '../ServerName.vue'
</script>

<template>
  <div>
    <ServerName />
    <div class="flex flex-col">
      <RouterLink
        :to="{ name: 'serverProfile' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        >Server Profile</RouterLink
      >
      <RouterLink
        :to="{ name: 'serverMembers' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        >Server Members</RouterLink
      >
    </div>
  </div>
</template>
