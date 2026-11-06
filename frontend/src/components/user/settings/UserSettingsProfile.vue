<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import { dateTimeMapper } from '@/services/common/utils'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'

  const userInfo = userInfoStore()
  const { user } = storeToRefs(userInfo)
</script>

<template>
  <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'userSettings' }">
    User Profile:
  </GoBackLink>
  <h1 class="mt-2 ml-2 hidden text-2xl font-semibold md:block">User Profile:</h1>
  <div class="flex flex-col px-4 py-2 text-lg md:p-4" v-if="user">
    <div class="pb-2 border-bottom-1">
      <div class="font-semibold">Username:</div>
      <div>{{ user.username }}</div>
    </div>
    <div class="py-2 border-bottom-1">
      <div class="font-semibold">Email:</div>
      <div>{{ user.email }}</div>
    </div>
    <div class="py-2 border-bottom-1" v-if="user.createdAt">
      <div class="font-semibold">Account Created:</div>
      <div>{{ dateTimeMapper(user.createdAt) }}</div>
    </div>
  </div>
</template>
