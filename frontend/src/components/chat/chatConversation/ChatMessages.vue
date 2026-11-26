<script setup lang="ts">
  import FilePreviewList from '@/components/shared/filePreview/FilePreviewList.vue'
  import UserTag from '@/components/shared/UserTag.vue'
  import { dateTimeMapper } from '@/services/common/utils'
  import { conversationStore } from '@/stores/conversation'
  import { userInfoStore } from '@/stores/userInfo'
  import type { ChatMessage } from '@/types'
  import { storeToRefs } from 'pinia'
  import { computed, ref } from 'vue'

  const conversation = conversationStore()

  const { currentMessages, pendingMessages, lastPage, currentConversationId } =
    storeToRefs(conversation)
  const { user } = storeToRefs(userInfoStore())

  // Pending messages of the open conversation, by the id of the message that sent them
  const pendingMessagesList = computed<Record<string, ChatMessage>>(() => {
    if (currentConversationId.value) {
      return pendingMessages.value[currentConversationId.value] ?? {}
    }
    return {}
  })

  const loadingOlder = ref(false)

  async function getOlderMessages() {
    if (loadingOlder.value) return

    loadingOlder.value = true
    try {
      await conversation.getNextMessages()
    } catch (err) {
      // TODO: show the error to the user. The button stays, so it can be retried.
    } finally {
      loadingOlder.value = false
    }
  }
</script>

<template>
  <div id="messages-list" class="flex min-h-full flex-col-reverse overflow-y-auto">
    <div
      class="pending-message px-4 opacity-25"
      v-for="(message, id) in pendingMessagesList"
      :key="id"
    >
      <div class="font-semibold">{{ user.username }} {{ dateTimeMapper(message.createdAt) }}</div>
      <div>
        {{ message.content }}
      </div>
    </div>

    <div
      v-for="message in currentMessages"
      :key="message.id"
      class="chat-message px-4 py-2 border-bottom-1-dark"
      :class="{ 'background-soft': message.author.username == user.username }"
    >
      <div class="flex items-center">
        <UserTag :user="message.author"></UserTag>
        <span class="px-4 text-sm"> {{ dateTimeMapper(message.createdAt) }} </span>
      </div>

      <FilePreviewList
        v-if="message.attachments?.length"
        :files="message.attachments"
        :can-download="true"
      />
      <div class="pl-4">
        {{ message.content }}
      </div>
    </div>
    <button
      type="button"
      @click="getOlderMessages"
      v-if="lastPage?.next"
      :disabled="loadingOlder"
      class="get-more-messages-btn pointer mx-4 my-2 rounded-sm border-1 background-soft px-4 py-2"
    >
      Get Older Messages
    </button>
  </div>
</template>

<style>
  .get-more-messages-btn:hover {
    background-color: rgb(var(--color-background-light));
  }
</style>
