<script setup lang="ts">
  import FilePreviewList from '@/components/shared/filePreview/FilePreviewList.vue'
  import wsMessageService from '@/services/common/websocketsMessage/WebSocketsMessageService'
  import MediaFileService from '@/services/mediaFile/MediaFileService'
  import { conversationStore } from '@/stores/conversation'
  import type { ChatMessage, MessageAttachment } from '@/types'
  import { computed, ref } from 'vue'

  const { chatName } = defineProps<{
    chatName: string
  }>()

  const conversation = conversationStore()

  const message = ref('')
  const fileInput = ref<HTMLInputElement>()
  const attachments = ref<MessageAttachment[]>([])
  const uploading = ref(false)

  const canSend = computed(
    () => !uploading.value && (message.value.trim() !== '' || attachments.value.length > 0)
  )

  async function sendMessage() {
    const conversationId = conversation.currentConversationId
    if (!conversationId || !canSend.value) return

    // The input is emptied at once, so the user can keep writing while the message is sent
    const content = message.value.trim()
    const sentAttachments = attachments.value
    message.value = ''
    attachments.value = []

    const chatMessage = {
      content,
      attachments: sentAttachments.map((x) => ({ mediaFileId: x.mediaFileId })),
    } as ChatMessage

    let ackId: string | undefined
    try {
      const ack = wsMessageService.sendMessage(conversationId, chatMessage)
      ackId = ack.id
      // Shown as pending, with the time it was sent, until the ack arrives
      conversation.addPendingMessage(ack.id, {
        ...chatMessage,
        createdAt: new Date().toISOString(),
      })

      conversation.confirmMessage(await ack.promise)
    } catch (err) {
      // Not sent: the message goes back to the input so it can be sent again
      if (ackId) conversation.removePendingMessage(ackId)
      if (message.value === '') message.value = content
      attachments.value = [...sentAttachments, ...attachments.value]
    }
  }

  function selectFile() {
    fileInput.value?.click()
  }

  async function uploadFile() {
    const input = fileInput.value
    const file = input?.files?.item(0)
    if (!input || !file) return

    uploading.value = true
    try {
      const response = await MediaFileService.uploadFile(file)
      if (response)
        attachments.value.push({
          mediaFileId: response.id,
          contentType: response.contentType,
          url: response.url,
          fileName: response.fileName,
        })
    } catch (err) {
      // TODO: show the error to the user
    } finally {
      uploading.value = false
      // Lets the same file be selected again
      input.value = ''
    }
  }

  function removeFile(file: MessageAttachment) {
    attachments.value = attachments.value.filter((x) => x.mediaFileId != file.mediaFileId)
  }
</script>

<template>
  <div class="max-w-screen pt-2 border-top-1">
    <FilePreviewList
      v-if="attachments.length > 0"
      :files="attachments"
      :canRemove="true"
      @remove="removeFile"
    />
    <div class="m-4 mt-0">
      <div
        class="max-width-full m-0 flex h-12 items-center rounded-[10px] border-1 background-primary p-3 md:mx-auto md:max-w-[80%]"
      >
        <div id="message-box-icons" class="flex items-center">
          <button
            type="button"
            class="inline-btn material-symbols-sharp pointer"
            aria-label="Attach file"
            :disabled="uploading"
            @click="selectFile"
          >
            add
          </button>
          <input id="file-input" ref="fileInput" type="file" hidden @change="uploadFile" />
        </div>
        <input
          v-model="message"
          type="text"
          id="message-input"
          aria-label="Write a message"
          class="h-full grow! pl-4!"
          @keyup.enter="sendMessage"
          :placeholder="`Write a message to ${chatName}`"
          autocomplete="off"
        />
        <button
          type="button"
          class="inline-btn pointer material-symbols-sharp"
          id="wsmessage-btn"
          aria-label="Send message"
          :disabled="!canSend"
          @click="sendMessage"
        >
          send
        </button>
      </div>
    </div>
  </div>
</template>

<style>
  #message-input {
    all: unset;
  }
</style>
