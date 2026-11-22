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

  async function getSentFriendRequests(request: PageRequest): Promise<PageResult<FriendRequest>> {
    return await FriendsService.getSentFriendRequests(request)
  }

  // Ids of the friend requests being revoked, to disable their button and ignore double clicks
  const revoking = ref(new Set<string>())

  async function revokeFriendRequest(friendRequest: { id: string }) {
    if (revoking.value.has(friendRequest.id)) return

    revoking.value.add(friendRequest.id)
    try {
      await FriendsService.updateFriendRequest({
        id: friendRequest.id,
        status: InvitationStatus.Revoked,
      })
      // Reloads the current page with the table's own loading state and error handling
      await table.value?.refresh()
    } catch (err) {
      // TODO: show the error to the user. The friend request stays as it was.
    } finally {
      revoking.value.delete(friendRequest.id)
    }
  }
</script>

<template>
  <div class="h-full w-full background-soft p-0 md:border-1 md:p-4">
    <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'friendRequests' }">
      Sent Friend Requests:
    </GoBackLink>

    <h1 class="mb-4 hidden text-2xl md:block">Sent Friend Requests:</h1>
    <div class="h-[80%] max-h-[80%]">
      <PaginatedTable
        :headers="[
          { key: 'username', name: 'Username', canSort: true, default: true },
          { key: 'status', name: 'Status', canSort: true, mapper: InvitationStatusMapper },
          { key: 'createdAt', name: 'Invited Date', canSort: true, mapper: dateTimeMapper },
          { key: 'modifiedAt', name: 'Last Update', canSort: true, mapper: dateTimeMapper },
        ]"
        :query-function="getSentFriendRequests"
        :has-actions="true"
        ref="table"
      >
        <template v-slot:actions="{ row }">
          <button
            class="pointer button-action py-1!"
            :disabled="revoking.has(row.id)"
            @click="async () => await revokeFriendRequest(row)"
          >
            Revoke
          </button>
        </template>
      </PaginatedTable>
    </div>
  </div>
</template>
