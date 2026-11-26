<script setup lang="ts">
  import { computed } from 'vue'
  import { presenceStore } from '@/stores/presence'

  const { userId } = defineProps<{ userId?: string | null }>()

  const presence = presenceStore()
  const online = computed(() => presence.isOnline(userId))
  // aria-hidden: it does not change the accessible name of the link or the row that contains it
</script>

<template>
  <span
    class="presence-indicator inline-block shrink-0 rounded-full"
    :class="online ? 'presence-online' : 'presence-offline'"
    :data-presence="online ? 'online' : 'offline'"
    :title="online ? 'Online' : 'Offline'"
    aria-hidden="true"
  ></span>
</template>

<style scoped>
  .presence-indicator {
    width: 0.625rem;
    height: 0.625rem;
  }

  .presence-online {
    background-color: #22c55e;
  }

  .presence-offline {
    border: 2px solid #9ca3af;
  }
</style>
