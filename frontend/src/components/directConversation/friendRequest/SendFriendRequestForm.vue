<script lang="ts" setup>
  import { getValidationErrors } from '@/services/common/utils'
  import { ApiError, ValidationError, type FriendRequest } from '@/types'
  import { computed, reactive, ref } from 'vue'
  import FriendsService from '@/services/friendsService/FriendsService'

  const emit = defineEmits<{
    (e: 'success', friendRequest: FriendRequest): void
  }>()

  const form = reactive({ username: '' })
  const submitting = ref(false)
  const error = ref('')

  const canSubmit = computed(() => !submitting.value && form.username.trim() !== '')

  const formErrors = ref(new Map<string, Map<string, string>>())

  async function onSubmit() {
    if (!canSubmit.value) return

    error.value = ''
    formErrors.value = new Map()
    submitting.value = true

    try {
      const friendRequest = await FriendsService.sendFriendRequest({
        username: form.username.trim(),
      })
      cleanForm()
      emit('success', friendRequest)
    } catch (err) {
      error.value =
        err instanceof ApiError && err.status < 500
          ? err.message
          : 'Something went wrong. Please try again.'

      if (err instanceof ValidationError && err.status == 400) {
        error.value = 'Please review the form.'
        formErrors.value = getValidationErrors(err)
      }
    } finally {
      submitting.value = false
    }
  }

  function cleanForm() {
    error.value = ''
    formErrors.value = new Map()
    form.username = ''
    submitting.value = false
  }
</script>

<template>
  <form class="custom-form" id="send-friend-request-form" novalidate @submit.prevent="onSubmit">
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <div class="custom-form-row">
      <div class="custom-form-label-wrapper">
        <label for="friend-request-username">Username *</label>
      </div>
      <div class="custom-form-input-wrapper">
        <input
          id="friend-request-username"
          class="custom-form-text-input"
          v-model="form.username"
          type="text"
          required
          placeholder="Username"
          minlength="3"
          maxlength="50"
          autocomplete="off"
        />
      </div>

      <p
        v-for="err in formErrors.get('Username')?.values()"
        :key="err"
        class="custom-form-field-error"
      >
        {{ err }}
      </p>

      <p class="mt-2">Please check the username before sending a request.</p>
      <p>You can revoke a friend request at any time.</p>
    </div>

    <button class="button-action" type="submit" :disabled="!canSubmit">Send Friend Request</button>
  </form>
</template>
