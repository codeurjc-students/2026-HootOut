<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import { dateTimeMapper } from '@/services/common/utils'
  import FriendsService from '@/services/friendsService/FriendsService'
  import {
    InvitationStatus,
    InvitationStatusMapper,
    type FriendRequest,
    type PageRequest,
    type PageResult,
  } from '@/types'
  import { ref } from 'vue'

  const table = ref<InstanceType<typeof PaginatedTable> | null>(null)

  async function getReceivedFriendRequests(
    request: PageRequest
  ): Promise<PageResult<FriendRequest>> {
    return await FriendsService.getReceivedFriendRequests(request)
  }

  // Ids of the friend requests being updated, to disable their buttons and ignore double clicks
  const updating = ref(new Set<string>())

  // The friends list loads again when the direct messages view is opened: no reload needed
  async function updateFriendRequest(friendRequest: { id: string }, status: InvitationStatus) {
    if (updating.value.has(friendRequest.id)) return

    updating.value.add(friendRequest.id)
    try {
      await FriendsService.updateFriendRequest({ id: friendRequest.id, status })
      // Reloads the current page with the table's own loading state and error handling
      await table.value?.refresh()
    } catch (err) {
      // TODO: show the error to the user. The friend request stays as it was.
    } finally {
      updating.value.delete(friendRequest.id)
    }
  }

  const acceptFriendRequest = (row: { id: string }) =>
    updateFriendRequest(row, InvitationStatus.Accepted)
  const rejectFriendRequest = (row: { id: string }) =>
    updateFriendRequest(row, InvitationStatus.Rejected)
</script>

<template>
  <div class="h-full w-full background-soft p-0 md:border-1 md:p-4">
    <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'friendRequests' }">
      Received Friend Requests:
    </GoBackLink>

    <h1 class="mb-4 hidden text-2xl md:block">Received Friend Requests:</h1>
    <div class="h-[80%] max-h-[80%]">
      <PaginatedTable
        :headers="[
          { key: 'invitedByUsername', name: 'Username', canSort: true, default: true },
          { key: 'status', name: 'Status', canSort: true, mapper: InvitationStatusMapper },
          { key: 'createdAt', name: 'Invited Date', canSort: true, mapper: dateTimeMapper },
          { key: 'modifiedAt', name: 'Last Update', canSort: true, mapper: dateTimeMapper },
        ]"
        :query-function="getReceivedFriendRequests"
        :has-actions="true"
        ref="table"
      >
        <template v-slot:actions="{ row }">
          <div class="flex justify-center gap-2">
            <button
              class="pointer button-action py-1!"
              :disabled="updating.has(row.id)"
              @click="async () => await acceptFriendRequest(row)"
            >
              Accept
            </button>
            <button
              class="pointer button-action py-1!"
              :disabled="updating.has(row.id)"
              @click="async () => await rejectFriendRequest(row)"
            >
              Reject
            </button>
          </div>
        </template>
      </PaginatedTable>
    </div>
  </div>
</template>
