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
  const { channels, currentChatChannel, currentServer } = storeToRefs(serverInfo) as any

  const channelToEdit = ref<ChatChannel>()
  const updateChannelModal = useTemplateRef('update-channel-form-modal')

  async function selectChannel(channel: ChatChannel) {
    router.push({ name: 'channel', params: { serverId: currentServer.id, channelId: channel.id } })
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

    await serverInfo.fetchServerChannels(currentServer.value)

    if (channel?.id != id) {
      serverInfo.setCurrentChatChannel(channel)
    }
  }
</script>

<template>
  <div class="channel-list">
    <div
      v-for="channel in channels"
      class="channel-name-row pointer"
      :class="{ 'background-light': channel.id == currentChatChannel?.id }"
      :key="channel.id"
    >
      <div @click="selectChannel(channel)" class="channel-name">{{ channel.name }}</div>

      <button
        @click="openUpdateChannelModal(channel)"
        type="button"
        class="inline-btn pointer material-symbols-sharp channel-settings"
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

<style>
  .channel-list {
    overflow-y: auto;
  }

  .channel-name-row {
    padding: 0rem 1rem;
    border-bottom: 1px solid rgb(var(--color-background-light));
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .channel-name-row:hover {
    background-color: rgb(var(--color-background-light));
  }

  .channel-name {
    flex-grow: 1;
    padding: 0.5rem 0;
  }

  .channel-settings {
    font-size: 1.25rem;
  }
</style>
