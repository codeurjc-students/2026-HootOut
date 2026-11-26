<script setup lang="ts">
  import type { UserInfo } from '@/types'
  import RoundedProfilePicture from './filePreview/RoundedProfilePicture.vue'
  import PresenceIndicator from './presence/PresenceIndicator.vue'
  import wsMessageService from '@/services/common/websocketsMessage/WebSocketsMessageService.ts'
  import { computed, onMounted } from 'vue'

  const { user, userId } = defineProps<{ user: UserInfo; userId?: string | undefined }>()

  onMounted(async () => {
    try {
      await wsMessageService.tryReconnect()
    } catch {
      // The members are shown offline; the automatic reconnection keeps trying
    }
  })

  const presenceId = computed(() => (userId ? userId : user.id))
</script>

<template>
  <div class="flex items-center" v-if="user">
    <div class="relative">
      <RoundedProfilePicture
        class="pointer material-symbols-sharp h-8 w-8 text-[1rem]!"
        :img-url="user.profilePicture?.url"
        alt="account_circle"
      ></RoundedProfilePicture>
      <PresenceIndicator class="absolute right-0 bottom-0" :user-id="presenceId" />
    </div>
    <div class="mx-2">{{ user?.username }}</div>
  </div>
</template>
