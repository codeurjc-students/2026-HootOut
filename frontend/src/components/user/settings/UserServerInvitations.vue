<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import ChatServerService from '@/services/chatServer/ChatServerService'
  import { dateTimeMapper } from '@/services/common/utils'
  import {
    InvitationStatus,
    type PageRequest,
    type PageResult,
    type ServerInvitation,
  } from '@/types'
  import { ref } from 'vue'
  import { useRouter } from 'vue-router'

  const table = ref<InstanceType<typeof PaginatedTable> | null>(null)
  const router = useRouter()

  async function getUserInvitations(request: PageRequest): Promise<PageResult<ServerInvitation>> {
    return await ChatServerService.getUserServerInvitations(request)
  }

  // Ids of the invitations being updated, to disable their buttons and ignore double clicks
  const updating = ref(new Set<string>())

  async function updateInvitation(invitation: { id: string }, status: InvitationStatus) {
    if (updating.value.has(invitation.id)) return

    updating.value.add(invitation.id)
    try {
      await ChatServerService.updateServerInvitation({ id: invitation.id, status })

      if (status == InvitationStatus.Accepted) {
        // Reloads the app so the new server appears in the server list
        router.go(0)
      } else {
        // Reloads the current page with the table's own loading state and error handling
        await table.value?.refresh()
      }
    } catch (err) {
      // TODO: show the error to the user. The invitation stays as it was.
    } finally {
      updating.value.delete(invitation.id)
    }
  }
</script>

<template>
  <div class="h-full w-full p-4">
    <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'userSettings' }">
      Server Invitations:
    </GoBackLink>
    <h1 class="mt-2 ml-2 hidden text-2xl font-semibold md:block">Server Invitations:</h1>
    <div class="h-[80%] max-h-[80%]">
      <PaginatedTable
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
              :disabled="updating.has(row.id)"
              @click="async () => await updateInvitation(row, InvitationStatus.Accepted)"
            >
              Join
            </button>
            <button
              class="pointer button-action py-1!"
              :disabled="updating.has(row.id)"
              @click="async () => await updateInvitation(row, InvitationStatus.Rejected)"
            >
              Reject
            </button>
          </div>
        </template>
      </PaginatedTable>
    </div>
  </div>
</template>
