<script setup lang="ts">
  import FriendsService from '@/services/friendsService/FriendsService'
  import { computed, ref } from 'vue'
  import { storeToRefs } from 'pinia'
  import InfiniteScrollList from '../shared/infiniteScroll/InfiniteScrollList.vue'
  import { directConversationStore, type FriendConversation } from '@/stores/directConversation'

  const friends = ref<FriendConversation[]>([])
  const dc = directConversationStore()
  const { currentConversation } = storeToRefs(dc)

  // Each page is added after the ones already loaded, without friends that are already there
  async function getFriendsList(next: string | null): Promise<string | null> {
    const page = await FriendsService.getUserFriends({ next })
    const loaded = new Set(friends.value.map((x) => x.directConversationId))
    friends.value.push(...page.items.filter((x) => !loaded.has(x.directConversationId)))
    return page.next ?? null
  }

  // Same as the server list: the open conversation goes first while it is not in the pages
  // loaded (e.g. after a refresh, with the data the view requested for the url).
  // When its page is loaded it is shown in its place, so it never appears twice
  const friendsList = computed<FriendConversation[]>(() => {
    const result: FriendConversation[] = []
    const current = currentConversation.value
    if (
      current &&
      !friends.value.some((x) => x.directConversationId == current.directConversationId)
    ) {
      result.push(current)
    }
    result.push(...friends.value)
    return result
  })

  function updateCurrentConversation(item: FriendConversation) {
    dc.selectFriendConversation(item)
  }

  const asFriendConversation = (item: unknown) => item as FriendConversation
</script>

<template>
  <InfiniteScrollList :items="friendsList" :load-more="getFriendsList" class="flex flex-col">
    <template v-slot:scroll-item="{ item }">
      <RouterLink
        :to="{
          name: 'directConversation',
          params: { directConversationId: asFriendConversation(item).directConversationId },
        }"
        class="pointer flex items-center justify-between px-4 py-2 border-bottom-1 hover:background-light"
        active-class="background-light"
        @click="updateCurrentConversation(asFriendConversation(item))"
      >
        {{ asFriendConversation(item).friend.username }}
      </RouterLink>
    </template>
  </InfiniteScrollList>
</template>
