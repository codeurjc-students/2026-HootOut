<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import Modal from '../shared/modal/Modal.vue'
  import { useTemplateRef } from 'vue'
  import InviteMemberForm from './InviteMemberForm.vue'

  const serverInfo = serverInfoStore()
  const { currentServer } = storeToRefs(serverInfo) as any

  const inviteMemberModal = useTemplateRef('invite-member-modal')

  function openInviteToServerModal() {
    inviteMemberModal.value?.open()
  }

  function closeInviteModal() {
    inviteMemberModal.value?.close()
  }
</script>

<template>
  <div class="server-name border-bottom-1 flex justify-between p-4 text-xl font-semibold">
    <div>{{ currentServer?.name }}</div>
    <div class="flex items-center gap-2">
      <button @click="openInviteToServerModal" type="button" class="inline-btn pointer">
        <span class="material-symbols-sharp text-xl!"> group_add </span>
      </button>
      <RouterLink
        :to="{ name: 'serverProfile', params: { serverId: currentServer.id } }"
        class="inline-btn pointer"
      >
        <span class="material-symbols-sharp text-xl!"> settings </span>
      </RouterLink>
    </div>
    <Modal title="Invite Member" ref="invite-member-modal">
      <template v-slot:body>
        <InviteMemberForm @success="" :server="currentServer" />
      </template>
    </Modal>
  </div>
</template>
