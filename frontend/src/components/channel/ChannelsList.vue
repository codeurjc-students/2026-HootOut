<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import type { ChatChannel } from '@/types'
  import { storeToRefs } from 'pinia'
  import UpdateChannelForm from './UpdateChannelForm.vue'
  import { ref, useTemplateRef } from 'vue'
  import Modal from '../shared/modal/Modal.vue'
  import { useRouter } from 'vue-router'

  const router = useRouter()

  const serverInfo = serverInfoStore()
  const { channels, currentChatChannel, currentServer } = storeToRefs(serverInfo)

  const channelToEdit = ref<ChatChannel>()
  const updateChannelModal = useTemplateRef('update-channel-form-modal')

  async function selectChannel(channel: ChatChannel) {
    router.push({
      name: 'channel',
      params: { serverId: currentServer.value?.id, channelId: channel.id },
    })
  }

  async function onUpdatedChannelSuccess(channel: ChatChannel) {
    updateChannelModal.value?.close()
    if (channelToEdit?.value) channelToEdit.value.name = channel.name
    selectChannel(channel)
  }

  function openUpdateChannelModal(channel: ChatChannel) {
    channelToEdit.value = channel
    updateChannelModal.value?.open()
  }

  async function onChannelDeleted(id: string) {
    updateChannelModal.value?.close()
    const channel = currentChatChannel.value

    await serverInfo.fetchServerChannels(currentServer.value!, true)

    if (channel?.id != id) {
      serverInfo.setCurrentChatChannel(channel)
    } else {
      router.push({ name: 'server', params: { serverId: currentServer.value!.id } })
    }
  }
</script>

<template>
  <div class="mb-4 overflow-y-auto">
    <div
      v-for="channel in channels"
      class="channel-name-row pointer flex items-center justify-between px-4 border-bottom-1 hover:background-light"
      :class="{ 'background-light': channel.id == currentChatChannel?.id }"
      :key="channel.id"
    >
      <RouterLink
        :to="{ name: 'channel', params: { serverId: currentServer!.id, channelId: channel.id } }"
        class="channel-name flex-1 py-2"
        >{{ channel.name }}
      </RouterLink>

      <button
        @click="openUpdateChannelModal(channel)"
        type="button"
        class="inline-btn pointer material-symbols-sharp text-xl!"
      >
        settings
      </button>
    </div>
  </div>
  <Modal title="Update Channel" ref="update-channel-form-modal">
    <template v-slot:body>
      <UpdateChannelForm
        :channel="channelToEdit"
        :server="currentServer"
        @success="onUpdatedChannelSuccess"
        @deleted="onChannelDeleted"
      />
    </template>
  </Modal>
</template>
