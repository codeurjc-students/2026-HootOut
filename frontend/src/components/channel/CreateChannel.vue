<script setup lang="ts">
  import { useTemplateRef } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useRouter } from 'vue-router'
  import CreateChannelForm from './CreateChannelForm.vue'
  import Modal from '../shared/modal/Modal.vue'
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { ChatChannel } from '@/types'

  const createChannelFormModal = useTemplateRef('create-channel-form-modal')
  const serverInfo = serverInfoStore()
  const { currentServer } = storeToRefs(serverInfo)
  const router = useRouter()

  async function onCreateChannelSuccess(newChannel: ChatChannel) {
    // false when the channel is already in the list: it is opened anyway
    serverInfo.addNewChannel(newChannel)
    createChannelFormModal.value?.close()

    // The channel view opens the channel (subscription and messages included)
    await router.push({
      name: 'channel',
      params: { serverId: newChannel.serverId, channelId: newChannel.id },
    })
  }

  function openCreateChannelModal() {
    createChannelFormModal.value?.open()
  }
</script>

<template>
  <div class="add-channel flex justify-between p-4 border-bottom-1">
    <span>Channels</span>
    <button
      v-if="currentServer"
      @click="openCreateChannelModal"
      type="button"
      class="inline-btn pointer material-symbols-sharp"
      aria-label="Create channel"
    >
      add
    </button>
  </div>
  <Modal title="Create New Channel" ref="create-channel-form-modal">
    <template v-slot:body>
      <CreateChannelForm :server="currentServer" @success="onCreateChannelSuccess" />
    </template>
  </Modal>
</template>
