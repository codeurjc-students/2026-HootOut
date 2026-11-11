<script setup lang="ts">
  import { serverInfoStore } from '@/stores/serverInfo'
  import { storeToRefs } from 'pinia'
  import Modal from '../shared/modal/Modal.vue'
  import { useTemplateRef } from 'vue'
  import InviteMemberForm from './InviteMemberForm.vue'

  const serverInfo = serverInfoStore()
  const { currentServer } = storeToRefs(serverInfo)

  const inviteMemberModal = useTemplateRef('invite-member-modal')

  function openInviteToServerModal() {
    inviteMemberModal.value?.open()
  }

  function closeInviteModal() {
    inviteMemberModal.value?.close()
  }
</script>

<template>
  <div class="server-name flex justify-between p-4 text-xl font-semibold border-bottom-1">
    <div>{{ currentServer?.name }}</div>
    <!-- Both actions need a server: they are not shown until there is one -->
    <template v-if="currentServer">
      <div class="flex items-center gap-2">
        <button
          @click="openInviteToServerModal"
          type="button"
          class="inline-btn pointer inviteMemberBtn"
          aria-label="Invite member"
        >
          <span class="material-symbols-sharp text-xl!"> group_add </span>
        </button>
        <RouterLink
          :to="{ name: 'serverProfile', params: { serverId: currentServer.id } }"
          class="inline-btn pointer"
          aria-label="Server settings"
        >
          <span class="material-symbols-sharp text-xl!"> settings </span>
        </RouterLink>
      </div>
      <Modal title="Invite Member" ref="invite-member-modal">
        <template v-slot:body>
          <InviteMemberForm @success="closeInviteModal" :server="currentServer" />
        </template>
      </Modal>
    </template>
  </div>
</template>
