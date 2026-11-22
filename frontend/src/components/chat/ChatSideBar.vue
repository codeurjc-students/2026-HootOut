<script setup lang="ts">
  import type { ChatServer } from '@/types'
  import Modal from '../shared/modal/Modal.vue'
  import CreateServerForm from '../server/CreateServerForm.vue'
  import { useTemplateRef } from 'vue'
  import ServerList from '../server/ServerList.vue'
  import { serverInfoStore } from '@/stores/serverInfo'
  import { useRouter } from 'vue-router'

  const createServerFormModal = useTemplateRef('create-server-form-modal')
  const serverstore = serverInfoStore()
  const router = useRouter()

  function openModal() {
    createServerFormModal.value?.open()
  }

  // The chat view loads the server of the route
  async function goToServer(server: ChatServer) {
    await router.push({ name: 'server', params: { serverId: server.id } })
  }

  async function onCreateServerSuccess(server: ChatServer) {
    serverstore.addChatServer(server)
    createServerFormModal.value?.close()

    await goToServer(server)
  }
</script>

<template>
  <aside class="side-bar grid h-full grid-rows-[auto_1fr_auto]">
    <div class="flex scrollbar-gutter-stable flex-col">
      <RouterLink
        id="directConversationLink"
        :to="{ name: 'directConversation' }"
        active-class="background-light"
        class="round-btn pointer al flex-column mb-2 flex h-full items-center justify-center self-center"
        v-tooltip:right="'Direct Messages'"
        aria-label="Direct Messages"
      >
        <span class="material-symbols-sharp">chat</span>
      </RouterLink>
    </div>
    <div class="separator relative my-2 flex items-center pl-2"></div>

    <div
      id="server-list"
      class="flex w-full grow scrollbar-gutter-stable flex-col overflow-x-hidden overflow-y-auto"
    >
      <ServerList />
    </div>

    <div
      id="new-server"
      class="separator relative flex scrollbar-gutter-stable items-center justify-center py-4"
    >
      <button
        id="new-server-btn"
        type="button"
        class="round-btn pointer"
        aria-label="Create server"
        @click="openModal"
      ></button>
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

  .separator::before {
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
