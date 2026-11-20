<script setup lang="ts">
  import GoBackLink from '@/components/shared/GoBackLink.vue'
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
  import router from '@/router'
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

  // Ids of the invitations being revoked, to disable their button and ignore double clicks
  const revoking = ref(new Set<string>())

  async function updateFriendRequest(friendRequest: any, status: InvitationStatus) {
    await FriendsService.updateFriendRequest({
      id: friendRequest.id,
      status: status,
    })

    if (status == InvitationStatus.Accepted) {
      router.go(0)
    } else {
      await table.value?.getPage(0)
    }
  }
</script>

<template>
  <div class="h-full w-full background-soft p-0 md:border-1 md:p-4">
    <GoBackLink class="p-2 text-sm font-semibold" :to="{ name: 'friendRequests' }">
      Received Friend Requests:
    </GoBackLink>
    <h2 class="px-4 pt-0 pb-2 text-sm font-semibold md:hidden">Received Friend Requests</h2>

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
              @click="async () => await updateFriendRequest(row, InvitationStatus.Accepted)"
            >
              Accept
            </button>
            <button
              class="pointer button-action py-1!"
              @click="async () => await updateFriendRequest(row, InvitationStatus.Rejected)"
            >
              Reject
            </button>
          </div>
        </template>
      </PaginatedTable>
    </div>
  </div>
</template>
