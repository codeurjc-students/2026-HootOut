<script setup lang="ts">
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { dateTimeMapper } from '@/services/common/utils'
  import { serverInfoStore } from '@/stores/serverInfo'
  import {
    ServerInvitationStatus,
    serverInvitationStatusMapper,
    type PageRequest,
    type PageResult,
    type ServerInvitation,
  } from '@/types'
  import { storeToRefs } from 'pinia'
  import { ref } from 'vue'

  const table = ref<InstanceType<typeof PaginatedTable> | null>(null)
  const { currentServer } = storeToRefs(serverInfoStore())

  async function getServerInvitations(request: PageRequest): Promise<PageResult<ServerInvitation>> {
    return await ChatServerService.getServerInvitations(currentServer.value!.id, request)
  }

  async function revokeInvitation(invitation: any) {
    await ChatServerService.updateServerInvitation({
      id: invitation.id,
      status: ServerInvitationStatus.Revoked,
    })

    await table.value?.getPage(0)
  }
</script>

<template>
  <div class="h-full w-full border-1 background-soft p-4">
    <h1 class="py- mb-4 text-2xl">Server Invitations</h1>
    <div class="h-[80%] max-h-[80%]">
      <PaginatedTable
        :headers="[
          { key: 'username', name: 'Username', canSort: true, default: true },
          { key: 'status', name: 'Status', canSort: true, mapper: serverInvitationStatusMapper },
          { key: 'createdAt', name: 'Invited Date', canSort: true, mapper: dateTimeMapper },
          { key: 'modifiedAt', name: 'Last Update', canSort: true, mapper: dateTimeMapper },
        ]"
        :query-function="getServerInvitations"
        :has-actions="true"
        ref="table"
      >
        <template v-slot:actions="{ row }">
          <button
            class="pointer button-action py-1!"
            @click="async () => await revokeInvitation(row)"
            v-if="row.status.value == ServerInvitationStatus.Pending"
          >
            Revoke
          </button>
        </template>
      </PaginatedTable>
    </div>
  </div>
</template>
