<script setup lang="ts">
import { useTemplateRef } from 'vue';
import CreateChannelForm from './CreateChannelForm.vue';
import Modal from '../shared/modal/Modal.vue';
import { serverInfoStore } from '@/stores/serverInfo.ts';
import { storeToRefs } from 'pinia';
import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService.ts';
import type { ChatChannel } from '@/types/chatChannel.ts';
import type { WebSocketMessage } from '@/types/websocketMessage.ts';

const createChannelFormModal = useTemplateRef('create-channel-form-modal');
const serverInfo = serverInfoStore();
const { currentServer } = storeToRefs(serverInfo)

async function onCreateChannelSuccess(newChannel: ChatChannel) {

    if (!serverInfo.addNewChannel(newChannel)) {
        return;
    }

    wsMessageService.subscribe(newChannel.id, (message: WebSocketMessage) => {
        serverInfo.newMessage(message)
    });

    serverInfo.setCurrentChatChannel(newChannel);

    createChannelFormModal.value?.close();
}

function openCreateChannelModal() {
    createChannelFormModal.value?.open();
}
</script>

<template>
    <div class="add-channel">
        <span>Channels</span>
        <button @click="openCreateChannelModal" type="button" class="inline-btn pointer"> + </button>
    </div>
    <Modal title="Create New Channel" ref="create-channel-form-modal">
        <template v-slot:body>
            <CreateChannelForm :server="currentServer" @success="onCreateChannelSuccess" />
        </template>
    </Modal>
</template>

<style>
.add-channel {
    padding: 1rem 1rem;
    display: flex;
    justify-content: space-between;
    border-bottom: 1px solid rgb(var(--color-background-light));
}
</style>