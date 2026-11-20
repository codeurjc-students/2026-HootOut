<script setup lang="ts">
  import FriendsService from '@/services/friendsService/FriendsService'
  import { conversationStore } from '@/stores/conversation'
  import type { UserInfo } from '@/types'
  import { storeToRefs } from 'pinia'
  import { onMounted, ref } from 'vue'
  import InfiniteScrollList from '../shared/infiniteScroll/InfiniteScrollList.vue'

  const friends = ref<UserInfo[]>([])

  const conversation = conversationStore()
  const { currentConversationId } = storeToRefs(conversation)

  onMounted(async () => {
    friends.value = []
  })

  async function getFriendsList(next: string | null) {
    const page = await FriendsService.getUserFriends({ next })
    friends.value = page.items
    return page?.next
  }
</script>

<template>
  <InfiniteScrollList :items="friends" :load-more="getFriendsList" class="flex flex-col">
    <template v-slot:scroll-item="{ item }">
      <RouterLink
        :to="{
          name: 'directConversation',
          params: { chatConversationId: (item as any).chatConversationId },
        }"
        class="pointer flex items-center justify-between px-4 py-2 border-bottom-1 hover:background-light"
        :class="{
          'background-light': currentConversationId == (item as any).chatConversationId,
        }"
        :key="(item as any).id"
        :aria-label="(item as any).friend.username"
        active-class="background-light"
      >
        {{ (item as any).friend.username }}
      </RouterLink>
    </template>
  </InfiniteScrollList>
</template>
