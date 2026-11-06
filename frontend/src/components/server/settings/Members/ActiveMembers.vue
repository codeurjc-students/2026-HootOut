<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { dateTimeMapper } from '@/services/common/utils'
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { PageResult, ServerMember, PageRequest } from '@/types'
  import { storeToRefs } from 'pinia'

  const { currentServer } = storeToRefs(serverInfoStore())

  async function getServerMembers(request: PageRequest): Promise<PageResult<ServerMember>> {
    return await ChatServerService.getServerMembers(currentServer.value!.id, request)
  }
</script>

<template>
  <div class="h-full w-full border-1 background-soft p-4">
    <GoBackLink class="p-2" :to="{ name: 'serverMembers' }"> Server Members: </GoBackLink>
    <h1 class="py- mb-4 hidden text-2xl md:block">Server Members:</h1>
    <div class="h-[80%] max-h-[80%]">
      <PaginatedTable
        :headers="[
          { key: 'username', name: 'Username', canSort: true, default: true },
          { key: 'createdAt', name: 'Joined At', canSort: true, mapper: dateTimeMapper },
        ]"
        :query-function="getServerMembers"
      />
    </div>
  </div>
</template>
