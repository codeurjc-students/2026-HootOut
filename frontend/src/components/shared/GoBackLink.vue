<script setup lang="ts">
  import { RouterLink, type RouteLocationRaw } from 'vue-router'

  const { to } = defineProps<{
    to: RouteLocationRaw
  }>()
</script>

<template>
  <RouterLink :to="to" class="flex items-center pr-2 md:hidden!">
    <span class="material-symbols-sharp inline-block pr-2">chevron_left</span>
    <span class="font-semibold">
      <slot />
    </span>
  </RouterLink>
</template>
