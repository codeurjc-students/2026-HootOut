<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { computed } from 'vue'
  import { RouterLink, useRoute } from 'vue-router'

  const route = useRoute()

  // On mobile only one side is shown: the list on the friend requests page itself,
  // and the selected view (received or sent requests) on its child routes
  const mobileClass = computed(() =>
    route.name == 'friendRequests' ? 'settings-list' : 'settings-view'
  )
</script>

<template>
  <div
    class="friend-request-settings grid h-full overflow-hidden border-1 md:grid-cols-[200px_1fr]"
  >
    <div class="border-left-1 members-side-bar flex w-full grow flex-col" :class="mobileClass">
      <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'userSettings' }">
        Friend Requests:
      </GoBackLink>

      <h2 class="-mt-[1px] hidden p-4 text-xl font-semibold border-bottom-1 md:block">
        Friend Requests:
      </h2>
      <RouterLink
        :to="{ name: 'receivedFriendRequests' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Received Friend Requests</RouterLink
      >
      <RouterLink
        :to="{ name: 'sentFriendRequests' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Sent Friend Requests</RouterLink
      >
    </div>
    <div class="friend-requests-view h-full w-full background-soft" :class="mobileClass">
      <router-view />
    </div>
  </div>
</template>

<style>
  .friend-request-settings {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  .friend-requests-view {
    max-height: calc(100vh - var(--navbar-height));
  }

  .members-side-bar {
    max-height: calc(100vh - var(--navbar-height));
  }

  @media (width < 520px) {
    .friend-request-settings.friend-requests-view {
      grid-template-columns: 1fr;
    }

    .friend-request-settings.settings-list {
      grid-template-columns: 80px 1fr;
    }

    .friend-request-settings.friend-requests-view {
      display: none;
    }

    .friend-requests-view.settings-list {
      display: none;
    }
  }
</style>
