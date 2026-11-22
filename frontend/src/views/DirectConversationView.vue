<script setup lang="ts">
  import ChatConversationView from '@/components/chat/ChatConversationView.vue'
  import ChatSideBar from '@/components/chat/ChatSideBar.vue'
  import FriendsBar from '@/components/directConversation/FriendsBar.vue'
  import wsMessageService from '@/services/common/websocketsMessage/WebSocketsMessageService'
  import { conversationStore } from '@/stores/conversation'
  import { directConversationStore } from '@/stores/directConversation'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'
  import { computed, onMounted, watch } from 'vue'
  import { useRoute, useRouter } from 'vue-router'

  const route = useRoute()
  const router = useRouter()
  const conversation = conversationStore()
  const directConversation = directConversationStore()
  const { currentConversation } = storeToRefs(directConversation)
  const { user } = storeToRefs(userInfoStore())

  onMounted(async () => {
    await wsMessageService.tryReconnect()
  })

  // On mobile only one side is shown: the friends list, or the chat when a conversation is open
  const mobileClass = computed(() =>
    route.params.directConversationId ? 'view-chat' : 'view-channels'
  )

  watch(
    () => route.params.directConversationId,
    async (newId) => {
      conversation.clearConversation()

      if (!newId) {
        // No conversation open: stop receiving the messages of the previous one
        directConversation.clearDirectConversation()
        conversation.closeConversation()
        return
      }

      const directConversationId = newId as string
      let found = false
      try {
        found = await directConversation.updateDirectConversation(
          directConversationId,
          user.value?.id ?? ''
        )
      } catch (err) {
        // The conversation could not be loaded: same as not found
      }

      // The route changed while loading: the new route loads its own conversation
      if (route.params.directConversationId !== directConversationId) return

      if (!found || !currentConversation.value) {
        // The conversation does not exist or belongs to other users
        await router.replace({ name: 'directConversation' })
        return
      }

      await conversation.selectConversation(currentConversation.value.chatConversationId)
    },
    {
      immediate: true,
    }
  )
</script>

<template>
  <section class="chat-layout grid grid-cols-[100px_300px_1fr]" :class="mobileClass">
    <ChatSideBar class="chat-side-bar" :class="mobileClass" />
    <FriendsBar
      class="chat-left-bar grid grid-rows-[auto_auto_1fr] rounded-l-[30px] background-soft max-sm:rounded-[30px]"
      :class="mobileClass"
    ></FriendsBar>
    <ChatConversationView
      :class="mobileClass"
      :chat-name="currentConversation?.friend?.username"
      :back-link="{
        name: 'directConversation',
      }"
    />
  </section>
</template>

<style>
  .chat-layout {
    max-height: calc(100vh - var(--navbar-height));
    height: calc(100vh - var(--navbar-height));
  }

  .chat-left-bar {
    max-height: calc(100vh - var(--navbar-height));
  }

  @media (width < 520px) {
    .chat-layout.view-chat {
      grid-template-columns: 1fr;
    }

    .chat-layout.view-channels {
      grid-template-columns: 80px 1fr;
    }

    .chat-side-bar.view-chat {
      display: none;
    }

    .chat-left-bar.view-chat {
      display: none;
    }

    .chat-conversation.view-channels {
      display: none;
    }
  }
</style>
