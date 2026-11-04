<script setup lang="ts">
  import { dateTimeMapper } from '@/services/common/utils'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'

  const userInfo = userInfoStore()
  const { user } = storeToRefs(userInfo)
</script>

<template>
  <h1 class="text-2xl font-semibold">User Profile:</h1>
  <div class="flex flex-col p-4 text-lg" v-if="user">
    <div class="border-bottom-1 py-2">
      <div class="font-semibold">Username:</div>
      <div>{{ user.username }}</div>
    </div>
    <div class="border-bottom-1 py-2">
      <div class="font-semibold">Email:</div>
      <div>{{ user.email }}</div>
    </div>
    <div class="border-bottom-1 py-2" v-if="user.createdAt">
      <div class="font-semibold">Account Created:</div>
      <div>{{ dateTimeMapper(user.createdAt) }}</div>
    </div>
  </div>
</template>
