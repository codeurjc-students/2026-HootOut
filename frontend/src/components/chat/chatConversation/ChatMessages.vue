<script setup lang="ts">
  import FilePreviewList from '@/components/shared/filePreview/FilePreviewList.vue'
  import { dateTimeMapper } from '@/services/common/utils'
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
</script>

<template>
  <div id="messages-list" class="flex min-h-full flex-col-reverse overflow-y-auto">
    <div class="px-4 opacity-25" v-for="(message, index) in pendingMessagesList" :key="message.id">
      <div class="font-semibold">{{ user.username }} {{ dateTimeMapper(message.createdAt!) }}</div>
      <div>
        {{ message.content }}
      </div>
    </div>

    <div
      v-for="(message, index) in channelMessages"
      :key="message.id"
      class="border-bottom-1-dark px-4 py-2"
      :class="{ 'background-soft': message.author.username == user.username }"
    >
      <div>
        <span class="font-semibold"> {{ message.author.username }}</span>
        <span class="px-4 text-sm"> {{ dateTimeMapper(message.createdAt!) }} </span>
      </div>

      <FilePreviewList v-if="message.attachments.length > 0" :files="message.attachments" />
      <div class="pl-4">
        {{ message.content }}
      </div>
    </div>
    <div
      @click="serverInfo.getNextMessages"
      v-if="lastPage?.next"
      class="get-more-messages-btn pointer mx-4 my-2 rounded-sm border-1 background-soft px-4 py-2"
    >
      Get Older Messages
    </div>
  </div>
</template>

<style>
  .get-more-messages-btn:hover {
    background-color: rgb(var(--color-background-light));
  }
</style>
