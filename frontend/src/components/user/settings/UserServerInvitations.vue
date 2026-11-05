<script setup lang="ts">
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { dateTimeMapper } from '@/services/common/utils'
  import { userInfoStore } from '@/stores/userInfo'
  import {
    ServerInvitationStatus,
    type PageRequest,
    type PageResult,
    type ServerInvitation,
  } from '@/types'
  import { storeToRefs } from 'pinia'
  import { ref } from 'vue'

  const userInfo = userInfoStore()
  const { user } = storeToRefs(userInfo)
  const InvitationStatus = ServerInvitationStatus // To fix build error TS2339: Property 'value' does not exist on type 'typeof ServerInvitationStatus'

  const table = ref<InstanceType<typeof PaginatedTable> | null>(null)

  async function getUserInvitations(request: PageRequest): Promise<PageResult<ServerInvitation>> {
    return await ChatServerService.getUserServerInvitations(request)
  }

  async function updateInvitation(invitation: any, status: ServerInvitationStatus) {
    await ChatServerService.updateServerInvitation({
      id: invitation.id,
      status: status,
    })

    await table.value?.getPage(0)
  }
</script>

<template>
  <h1 class="text-2xl font-semibold">Server Invitations:</h1>
  <div class="flex flex-col p-4 text-lg" v-if="user"></div>
  <div class="h-[80%] max-h-[80%]">
    <PaginatedTable
      class="background-s"
      :headers="[
        { key: 'serverName', name: 'Server', canSort: true, default: true },
        { key: 'username', name: 'Invited By', canSort: true },
        { key: 'createdAt', name: 'Invited Date', canSort: true, mapper: dateTimeMapper },
      ]"
      :query-function="getUserInvitations"
      :has-actions="true"
      ref="table"
    >
      <template v-slot:actions="{ row }">
        <div class="flex justify-center gap-2">
          <button
            class="pointer button-action py-1!"
            @click="async () => await updateInvitation(row, InvitationStatus.Accepted)"
          >
            Join
          </button>
          <button
            class="pointer button-action py-1!"
            @click="async () => await updateInvitation(row, InvitationStatus.Rejected)"
          >
            Reject
          </button>
        </div>
      </template>
    </PaginatedTable>
  </div>
</template>
