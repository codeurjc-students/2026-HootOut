<script setup lang="ts">
  import { useTemplateRef } from 'vue'
  import SendFriendRequestForm from './friendRequest/SendFriendRequestForm.vue'
  import FriendsList from './FriendsList.vue'
  import Modal from '../shared/modal/Modal.vue'

  const friendRequestModal = useTemplateRef('send-friend-request-modal')

  function openFriendRequestModal() {
    friendRequestModal.value?.open()
  }

  function closeFriendRequestModal() {
    friendRequestModal.value?.close()
  }
</script>

<template>
  <div>
    <div class="flex justify-between p-4 text-xl font-semibold border-bottom-1">
      <h2>Friends</h2>

      <button
        @click="openFriendRequestModal"
        type="button"
        class="inline-btn pointer"
        aria-label="Send friend request"
      >
        <span class="material-symbols-sharp text-xl!"> group_add </span>
      </button>
    </div>

    <Modal title="Send Friend Request" ref="send-friend-request-modal">
      <template v-slot:body>
        <SendFriendRequestForm @success="closeFriendRequestModal" />
      </template>
    </Modal>

    <FriendsList />
  </div>
</template>
