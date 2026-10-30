<script setup lang="ts">
  import type { ChatServer } from '@/types'
  import Modal from '../shared/modal/Modal.vue'
  import CreateServerForm from '../server/CreateServerForm.vue'
  import { useTemplateRef } from 'vue'
  import ServerList from '../server/ServerList.vue'
  import { serverInfoStore } from '@/stores/serverInfo.ts'
  import { useRouter } from 'vue-router'

  const createServerFormModal = useTemplateRef('create-server-form-modal')
  const serverstore = serverInfoStore()
  const router = useRouter()

  function openModal() {
    createServerFormModal.value?.open()
  }

  function setCurrentServer(server: ChatServer) {
    router.push({ name: 'server', params: { serverId: server.id } })
  }

  function onCreateServerSuccess(server: ChatServer) {
    serverstore.addChatServer(server)
    serverstore.setCurrentServer(server.id)
    createServerFormModal.value?.close()

    setCurrentServer(server)
  }
</script>

<template>
  <aside class="side-bar grid h-full grid-rows-[auto_1fr_auto]">
    <div id=""></div>
    <div id="server-list" class="flex w-full grow flex-col overflow-x-hidden overflow-y-auto">
      <ServerList />
    </div>

    <div id="new-server" class="relative flex items-center justify-center py-4">
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
  .side-bar {
    max-height: calc(100vh - var(--navbar-height));
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
