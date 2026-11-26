<script setup lang="ts">
  import RoundedProfilePicture from '@/components/shared/filePreview/RoundedProfilePicture.vue'
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { dateTimeMapper } from '@/services/common/utils'
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'

  const { currentServer } = storeToRefs(serverInfoStore())
</script>

<template>
  <div class="p-4">
    <GoBackLink :to="{ name: 'serverSettings' }"> Server Profile: </GoBackLink>
    <h1 class="mt-2 ml-2 hidden text-2xl font-semibold md:block">Server Settings:</h1>
    <div class="flex flex-col p-2 text-lg md:p-4" v-if="currentServer">
      <div class="py-2 border-bottom-1">
        <div class="font-semibold">Server Profile Picture:</div>
        <RoundedProfilePicture
          class="pointer m-4 h-40 w-40 text-[3rem]!"
          :img-url="currentServer.profilePicture?.url"
          :alt="currentServer.name.charAt(0).toUpperCase()"
          v-tooltip:right="currentServer.name"
        ></RoundedProfilePicture>
      </div>
      <div class="py-2 border-bottom-1">
        <div class="font-semibold">Server Name:</div>
        <div>{{ currentServer.name }}</div>
      </div>
      <div class="py-2 border-bottom-1">
        <div class="font-semibold">Server Description:</div>
        <div>{{ currentServer.description || 'No description' }}</div>
      </div>
      <div class="py-2 border-bottom-1" v-if="currentServer.createdAt">
        <div class="font-semibold">Server Created:</div>
        <div>{{ dateTimeMapper(currentServer.createdAt) }}</div>
      </div>
    </div>
  </div>
</template>
