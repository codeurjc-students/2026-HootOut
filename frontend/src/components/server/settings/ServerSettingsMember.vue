<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import { computed } from 'vue'
  import { RouterLink, useRoute } from 'vue-router'

  const route = useRoute()

  const { currentServer } = storeToRefs(serverInfoStore())

  // On mobile only one side is shown: the list on the members page itself,
  // and the selected view (active members, invitations...) on its child routes
  const mobileClass = computed(() =>
    route.name == 'serverMembers' ? 'settings-list' : 'settings-view'
  )
</script>

<template>
  <div class="members-settings grid h-full overflow-hidden border-1 md:grid-cols-[200px_1fr]">
    <div class="border-left-1 members-side-bar flex w-full grow flex-col" :class="mobileClass">
      <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'serverSettings' }">
        Server Members:
      </GoBackLink>
      <h2 class="px-4 pt-0 pb-2 text-sm font-semibold border-bottom-1 md:hidden">
        Server: {{ currentServer?.name }}
      </h2>

      <h2 class="mt-[1px] hidden p-4 text-xl font-semibold border-bottom-1 md:block">
        Server Members:
      </h2>
      <RouterLink
        :to="{ name: 'serverActiveMembers' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Active Members</RouterLink
      >
      <RouterLink
        :to="{ name: 'serverInvitations' }"
        class="pointer px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        >Server Invitations</RouterLink
      >
    </div>
    <div class="members-view h-full w-full background-soft" :class="mobileClass">
      <router-view />
    </div>
  </div>
</template>

<style>
  .members-settings {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  .members-view {
    max-height: calc(100vh - var(--navbar-height));
  }

  .members-side-bar {
    max-height: calc(100vh - var(--navbar-height));
  }

  @media (width < 520px) {
    .members-settings.settings-view {
      grid-template-columns: 1fr;
    }

    .members-settings.settings-list {
      grid-template-columns: 80px 1fr;
    }

    .members-side-bar.settings-view {
      display: none;
    }

    .members-view.settings-list {
      display: none;
    }
  }
</style>
