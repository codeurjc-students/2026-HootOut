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

  function onUpdatedChannelSuccess(channel: ChatChannel) {
    updateChannelModal.value?.close()
    // Only the name changes in the list: the user stays in the channel they are in
    if (channelToEdit.value) channelToEdit.value.name = channel.name
  }

  function openUpdateChannelModal(channel: ChatChannel) {
    channelToEdit.value = channel
    updateChannelModal.value?.open()
  }

  async function onChannelDeleted(id: string) {
    updateChannelModal.value?.close()
    const server = currentServer.value
    if (!server) return
    const openChannel = currentChatChannel.value

    try {
      // Reloads the channel list. It also closes the open channel and clears its messages
      await serverInfo.fetchServerChannels(server, true)

      if (openChannel?.id == id) {
        router.push({ name: 'server', params: { serverId: server.id } })
      } else if (openChannel) {
        // Open it again so its messages are loaded again
        await serverInfo.selectChannel(openChannel.id)
      }
    } catch (err) {
      // TODO: show the error to the user
    }
  }
</script>

<template>
  <div class="mb-4 overflow-y-auto" v-if="currentServer">
    <div
      v-for="channel in channels"
      class="channel-name-row pointer flex items-center justify-between px-4 border-bottom-1 hover:background-light"
      :class="{ 'background-light': channel.id == currentChatChannel?.id }"
      :key="channel.id"
    >
      <RouterLink
        :to="{ name: 'channel', params: { serverId: currentServer.id, channelId: channel.id } }"
        class="channel-name flex-1 py-2"
        >{{ channel.name }}
      </RouterLink>

      <button
        @click="openUpdateChannelModal(channel)"
        type="button"
        class="inline-btn pointer material-symbols-sharp text-xl!"
        :aria-label="`Edit channel ${channel.name}`"
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
