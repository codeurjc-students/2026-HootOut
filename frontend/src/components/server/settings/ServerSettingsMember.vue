<script setup lang="ts">
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { PageResult, ServerMember } from '@/types'
  import { storeToRefs } from 'pinia'
  import { onMounted, ref } from 'vue'

  const { currentServer } = storeToRefs(serverInfoStore())
  let currentPage: PageResult<ServerMember> | undefined = undefined
  const members = ref<ServerMember[]>([])

  onMounted(async () => {
    if (!currentServer.value) return

    currentPage = await ChatServerService.getServerMembers(currentServer.value?.id)
    members.value = currentPage.items
  })
</script>

<template>
  <table class="table-auto">
    <thead>
      <tr>
        <th>User Name</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="member in members">
        <td>{{ member.username }}</td>
      </tr>
    </tbody>
  </table>
</template>
