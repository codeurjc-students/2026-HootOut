<script setup lang="ts">
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { serverInfoStore } from '@/stores/serverInfo'
  import {
    ServerInvitationsStatusMapper,
    type PageRequest,
    type PageResult,
    type ServerInvitation,
  } from '@/types'
  import { storeToRefs } from 'pinia'

  const { currentServer } = storeToRefs(serverInfoStore())

  async function getServerInvitations(request: PageRequest): Promise<PageResult<ServerInvitation>> {
    return await ChatServerService.getServerInvitations(currentServer.value!.id, request)
  }
</script>

<template>
  <div class="h-full w-full border-1 background-soft p-4">
    <h1 class="py- mb-4 text-2xl">Server Invitations</h1>
    <div class="h-[60%] max-h-[60%]">
      <PaginatedTable
        :headers="[
          { key: 'username', name: 'Username', canSort: true, default: true },
          { key: 'status', name: 'Status', canSort: true, mapper: ServerInvitationsStatusMapper },
        ]"
        ,
        :query-function="getServerInvitations"
      />
    </div>
  </div>
</template>
