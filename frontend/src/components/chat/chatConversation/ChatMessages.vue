<script setup lang="ts">
  import FilePreviewList from '@/components/shared/filePreview/FilePreviewList.vue'
  import { serverInfoStore } from '@/stores/serverInfo'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'
  import { computed } from 'vue'

  const serverInfo = serverInfoStore()

  const { channelMessages, currentChatChannel, pendingMessages, lastPage } = storeToRefs(serverInfo)
  const { user } = storeToRefs(userInfoStore())

  const pendingMessagesList = computed(() => {
    return pendingMessages?.value[currentChatChannel.value!.id]
  })

  function convertDate(date_string: string) {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(date_string))
  }
</script>

<template>
  <div id="messages-list" class="flex min-h-full flex-col-reverse overflow-y-auto px-4">
    <div
      class="message-entry pending opacity-25"
      v-for="(message, index) in pendingMessagesList"
      :key="message.id"
    >
      <div>{{ user.username }} --- {{ convertDate(message.createdAt!) }}</div>
      <div>
        {{ message.content }}
      </div>
    </div>

    <div
      v-for="(message, index) in channelMessages"
      :key="message.id"
      :class="{ 'background-soft': message.author.username == user.username }"
    >
      <div>{{ message.author.username }} --- {{ convertDate(message.createdAt!) }}</div>

      <FilePreviewList v-if="message.attachments.length > 0" :files="message.attachments" />
      <div>
        {{ message.content }}
      </div>
    </div>
    <div
      @click="serverInfo.getNextMessages"
      v-if="lastPage?.next"
      class="get-more-messages-btn background-soft pointer my-2"
    >
      Get Older Messages
    </div>
  </div>
</template>

<style>
  .get-more-messages-btn {
    padding: 0.5rem 1rem;
    border-radius: var(--border-radius);
    border: 1px solid rgb(var(--color-border));
  }

  .get-more-messages-btn:hover {
    background-color: rgb(var(--color-background-light));
  }
</style>
