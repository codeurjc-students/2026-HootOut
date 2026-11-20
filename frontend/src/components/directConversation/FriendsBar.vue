<script setup lang="ts">
  import { useTemplateRef } from 'vue'
  import SendFriendRequestModal from './friendRequest/SendFriendRequestModal.vue'
  import FriendsList from './FriendsList.vue'
  import Modal from '../shared/modal/Modal.vue'

  const friendRequestModal = useTemplateRef('send-friend-request-modal')

  function openInviteToServerModal() {
    friendRequestModal.value?.open()
  }

  function closeFriendRequestModal() {
    friendRequestModal.value?.close()
  }
</script>

<template>
  <div>
    <div class="server-name flex justify-between p-4 text-xl font-semibold border-bottom-1">
      <h2 class="text-md font-semibold">Friends</h2>

      <button
        @click="openInviteToServerModal"
        type="button"
        class="inline-btn pointer inviteMemberBtn"
        aria-label="Invite member"
      >
        <span class="material-symbols-sharp text-xl!"> group_add </span>
      </button>
    </div>

    <Modal title="Send Friend Request" ref="send-friend-request-modal">
      <template v-slot:body>
        <SendFriendRequestModal @success="closeFriendRequestModal" />
      </template>
    </Modal>

    <FriendsList></FriendsList>
  </div>
</template>
