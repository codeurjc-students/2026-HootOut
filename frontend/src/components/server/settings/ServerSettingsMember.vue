<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { ref, watch } from 'vue'
  import { RouterLink, useRoute } from 'vue-router'

  const route = useRoute()

  const mobileClass = ref('settings-list')

  watch(
    () => route.name,
    async (name) => {
      if (name == 'serverMembers') {
        mobileClass.value = 'settings-list'
      } else {
        mobileClass.value = 'settings-view'
      }
    },
    {
      immediate: true,
    }
  )
</script>

<template>
  <div class="members-settings grid h-full border-1 md:grid-cols-[200px_1fr]">
    <div class="border-left-1 members-side-bar flex w-full flex-col" :class="mobileClass">
      <GoBackLink class="p-2" :to="{ name: 'serverSettings' }"> Server Members: </GoBackLink>
      <h2 class="border-bottom-1 mt-[1px] hidden p-4 text-xl font-semibold md:block">
        Server Members
      </h2>
      <RouterLink
        :to="{ name: 'serverActiveMembers' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.name == 'serverActiveMembers' ? 'background-light' : ''"
        >Active Members</RouterLink
      >
      <RouterLink
        :to="{ name: 'serverInvitations' }"
        class="pointer border-bottom-1 px-4 py-2 hover:background-light"
        :class="route.name == 'serverInvitations' ? 'background-light' : ''"
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

  @media (max-width: 520px) {
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
