<script setup lang="ts">
import type { ChatServer } from '@/types';
import Modal from '../shared/modal/Modal.vue';
import CreateServerForm from '../server/CreateServerForm.vue';
import { ref, useTemplateRef } from 'vue';
import ServerList from '../server/ServerList.vue';
import { serverInfoStore } from '@/stores/serverInfo.ts';

const createServerFormModal = useTemplateRef('create-server-form-modal');
const serverstore = serverInfoStore();

function openModal() {
    createServerFormModal.value?.open();
}

function onCreateServerSuccess(server: ChatServer) {
    serverstore.addChatServer(server)
    serverstore.setCurrentServer(server.id);
    createServerFormModal.value?.close();
}

</script>

<template>
    <aside id="side-bar">
        <div id=""></div>
        <div id="server-list">
            <ServerList />
        </div>

        <div id="new-server">
            <div id="new-server-btn" class="round-btn pointer" @click="openModal"></div>
            <Modal title="Create New Server" ref="create-server-form-modal">
                <template v-slot:body>
                    <CreateServerForm @success="onCreateServerSuccess" />
                </template>
            </Modal>
        </div>
    </aside>
</template>

<style>
#side-bar {
    height: 100%;
    max-height: calc(100vh - var(--navbar-height));
    display: grid;
    grid-template-rows: auto 1fr auto;
}

#server-list {
    display: flex;
    flex-direction: column;
    flex-grow: 1;
    width: 100%;
    overflow-y: auto;
    overflow-x: hidden;
}

#new-server {
    display: flex;
    padding: 1rem 0;
    align-items: center;
    justify-content: center;
    position: relative;
}

#new-server::before {
    content: '';
    width: 80%;
    height: 1px;
    background-color: rgb(var(--color-background-light));
    position: absolute;
    top: 0;
}

#new-server-btn::after {
    content: '+';
}
</style>