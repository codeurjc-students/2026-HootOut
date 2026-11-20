<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import type { RouteLocationRaw } from 'vue-router'

  const { backLink, chatName } = defineProps<{
    backLink: RouteLocationRaw
    chatName: String
  }>()
</script>

<template>
  <div class="chat-info flex background-light px-4 py-2">
    <GoBackLink :to="backLink">
      <div>
        {{ chatName }}
      </div>
    </GoBackLink>

    <div class="hidden font-semibold md:block">
      {{ chatName }}
    </div>
  </div>
</template>
