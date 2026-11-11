<script setup lang="ts">
  import ServerName from '../ServerName.vue'
</script>

<template>
  <div>
    <ServerName />
    <div class="flex flex-col">
      <!-- active-class: highlighted on its own route and on its child routes -->
      <RouterLink
        :to="{ name: 'serverProfile' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Server Profile</RouterLink
      >
      <RouterLink
        :to="{ name: 'serverMembers' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Server Members</RouterLink
      >
    </div>
  </div>
</template>
