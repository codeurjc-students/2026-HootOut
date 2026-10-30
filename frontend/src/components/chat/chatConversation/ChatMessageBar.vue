<script setup lang="ts">
  import FilePreviewList from '@/components/shared/filePreview/FilePreviewList.vue'
  import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
  import MediaFileService from '@/services/mediaFile/MediaFileService'
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { ChatMessage, MessageAttachment } from '@/types'
  import { storeToRefs } from 'pinia'
  import { ref } from 'vue'

  const serverInfo = serverInfoStore()
  const { currentChatChannel } = storeToRefs(serverInfo)

  const message = ref('')
  const fileInput = ref<HTMLInputElement>()
  const attachments = ref<MessageAttachment[]>([])

  async function sendMessage() {
    if (!message.value && attachments.value.length == 0) return
    const chatMessage: ChatMessage = {
      content: message.value,
      attachments: attachments.value.flatMap(
        (x) => <MessageAttachment>{ mediaFileId: x.mediaFileId }
      ),
    } as ChatMessage
    message.value = ''

    const ackPromise = await wsMessageService.sendMessage(
      serverInfo.currentChatChannel!,
      chatMessage
    )
    chatMessage.createdAt = new Date().toISOString()
    serverInfo.addPendingMessage(ackPromise.id, chatMessage)

    const wsMessage = await ackPromise.promise
    serverInfo.confirmMessage(wsMessage)

    attachments.value.length = 0
  }

  async function selectFile() {
    fileInput?.value?.click()
  }

  async function uploadFile() {
    const files = fileInput.value?.files
    if (!files || files.length == 0) {
      return
    }

    const response = await MediaFileService.uploadFile(files.item(0))
    if (response)
      attachments.value?.push({
        mediaFileId: response.id,
        contentType: response.contentType,
        url: response.url,
        fileName: response.fileName,
      })
  }

  function removeFile(file: MessageAttachment) {
    attachments.value = attachments.value?.filter((x) => x.mediaFileId != file.mediaFileId)
  }
</script>

<template>
  <div class="chat-message-bar max-w-screen pt-2">
    <FilePreviewList
      v-if="attachments.length > 0"
      :files="attachments"
      :canRemove="true"
      @remove="removeFile"
    />
    <div id="message-input-bar" class="m-4 mt-0">
      <div
        id="message-box"
        class="background-primary max-width-full m-0 flex h-12 items-center rounded-[10px] p-3 md:mx-auto md:max-w-[80%]"
      >
        <div id="message-box-icons" class="flex items-center">
          <div class="inline-btn material-symbols-sharp pointer" @click="selectFile">add</div>
          <input id="file-input" ref="fileInput" type="file" hidden @change="uploadFile" />
        </div>
        <input
          v-model="message"
          type="text"
          id="message-input"
          class="h-full grow! pl-4!"
          @keyup.enter="sendMessage"
          :placeholder="`Write a message to ${currentChatChannel?.name}`"
          autocomplete="off"
        />
        <div
          class="inline-btn pointer material-symbols-sharp"
          type="button"
          @click="sendMessage"
          id="wsmessage-btn"
        >
          send
        </div>
      </div>
    </div>
  </div>
</template>

<style>
  .chat-message-bar {
    border-top: 1px solid rgb(var(--color-border));
  }

  #message-box {
    border: 1px solid rgb(var(--color-border));
  }

  #message-input {
    all: unset;
  }
</style>
