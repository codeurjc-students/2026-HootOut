<script setup lang="ts">
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { PageResult, ServerMember, PageRequest } from '@/types'
  import { storeToRefs } from 'pinia'
  import { onMounted, ref } from 'vue'

  const { currentServer } = storeToRefs(serverInfoStore())
  const members = ref<ServerMember[]>([])

  async function getServerMembers(request: PageRequest): Promise<PageResult<ServerMember>> {
    return await ChatServerService.getServerMembers(currentServer.value!.id, request)
  }
</script>

<template>
  <div class="h-full w-full border-1 background-soft p-4">
    <h1 class="py- mb-4 text-2xl">Server Members</h1>
    <div class="h-[60%] max-h-[60%]">
      <PaginatedTable
        :items="members"
        :headers="[
          { key: 'username', name: 'Username', canSort: true, default: true },
          { key: 'createdAt', name: 'Joined At', canSort: true },
        ]"
        :query-function="getServerMembers"
      />
    </div>
  </div>
</template>
