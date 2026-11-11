<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
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

  // Ids of the invitations being revoked, to disable their button and ignore double clicks
  const revoking = ref(new Set<string>())

  async function revokeInvitation(invitation: any) {
    if (revoking.value.has(invitation.id)) return

    revoking.value.add(invitation.id)
    try {
      await ChatServerService.updateServerInvitation({
        id: invitation.id,
        status: ServerInvitationStatus.Revoked,
      })
      // Reloads the current page with the table's own loading state and error handling
      await table.value?.refresh()
    } catch (err) {
      // TODO: show the error to the user. The invitation stays as it was.
    } finally {
      revoking.value.delete(invitation.id)
    }
  }
</script>

<template>
  <div class="h-full w-full background-soft p-0 md:border-1 md:p-4">
    <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'serverMembers' }">
      Server Invitations:
    </GoBackLink>
    <h2 class="px-4 pt-0 pb-2 text-sm font-semibold md:hidden">
      Server: {{ currentServer?.name }}
    </h2>

    <h1 class="mb-4 hidden text-2xl md:block">Server Invitations:</h1>
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
            :disabled="revoking.has(row.id)"
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
