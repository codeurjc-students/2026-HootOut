<script setup lang="ts">
  import { useTemplateRef } from 'vue'
  import CreateChannelForm from './CreateChannelForm.vue'
  import Modal from '../shared/modal/Modal.vue'
  import { serverInfoStore } from '@/stores/serverInfo.ts'
  import { storeToRefs } from 'pinia'
  import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService.ts'
  import type { ChatChannel } from '@/types/chatChannel.ts'
  import type { WebSocketMessage } from '@/types/websocketMessage.ts'
  import { useRouter } from 'vue-router'

  const createChannelFormModal = useTemplateRef('create-channel-form-modal')
  const serverInfo = serverInfoStore()
  const { currentServer } = storeToRefs(serverInfo)
  const router = useRouter()

  async function selectChannel(channel: ChatChannel) {
    router.push({
      name: 'channel',
      params: { serverId: currentServer.value?.id, channelId: channel.id },
    })
  }

  async function onCreateChannelSuccess(newChannel: ChatChannel) {
    if (!serverInfo.addNewChannel(newChannel)) {
      return
    }

    wsMessageService.addPendingSubscription(newChannel.id, (message: WebSocketMessage) => {
      serverInfo.newMessage(message)
    })

    serverInfo.setCurrentChatChannel(newChannel)

    createChannelFormModal.value?.close()
    selectChannel(newChannel)
  }

  function openCreateChannelModal() {
    createChannelFormModal.value?.open()
  }
</script>

<template>
  <div class="add-channel border-bottom-1 flex justify-between p-4">
    <span>Channels</span>
    <button @click="openCreateChannelModal" type="button" class="inline-btn pointer">+</button>
  </div>
  <Modal title="Create New Channel" ref="create-channel-form-modal">
    <template v-slot:body>
      <CreateChannelForm :server="currentServer" @success="onCreateChannelSuccess" />
    </template>
  </Modal>
</template>
