<script setup lang="ts">
  import FriendsService from '@/services/friendsService/FriendsService'
  import type { FriendRelation } from '@/types'
  import { ref } from 'vue'
  import InfiniteScrollList from '../shared/infiniteScroll/InfiniteScrollList.vue'

  const friends = ref<FriendRelation[]>([])

  // Each page is added after the ones already loaded
  async function getFriendsList(next: string | null): Promise<string | null> {
    const page = await FriendsService.getUserFriends({ next })
    friends.value.push(...page.items)
    return page.next ?? null
  }

  const asFriendRelation = (item: unknown) => item as FriendRelation
</script>

<template>
  <InfiniteScrollList :items="friends" :load-more="getFriendsList" class="flex flex-col">
    <template v-slot:scroll-item="{ item }">
      <RouterLink
        :to="{
          name: 'directConversation',
          params: { chatConversationId: asFriendRelation(item).chatConversationId },
        }"
        class="pointer flex items-center justify-between px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
      >
        {{ asFriendRelation(item).friend.username }}
      </RouterLink>
    </template>
  </InfiniteScrollList>
</template>
