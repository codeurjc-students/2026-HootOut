<script setup lang="ts">
  import ChatChannelService from '@/services/chatChannel/ChatChannelService'
  import { getValidationErrors } from '@/services/common/utils'
  import { ApiError, ValidationError, type ChatChannel, type ChatServer } from '@/types'
  import { computed, reactive, ref, watch } from 'vue'

  defineExpose({
    cleanForm,
  })

  const props = defineProps<{
    channel?: ChatChannel | null
    server?: ChatServer | null
  }>()

  const emit = defineEmits<{
    (e: 'success', channel: ChatChannel): void
    (e: 'deleted', id: string): void
  }>()

  const form = reactive({ name: '' })
  const submitting = ref(false)
  const error = ref('')
  const formErrors = ref(new Map<string, Map<string, string>>())

  const hasChannel = computed(() => !!props.channel?.id?.trim())
  const canSubmit = computed(() => !submitting.value && form.name.trim() !== '' && hasChannel.value)
  const canDelete = computed(() => !submitting.value && hasChannel.value)

  // The form shows the name of the channel to edit, also when another channel is passed
  watch(
    () => props.channel,
    () => cleanForm(),
    { immediate: true }
  )

  function errorMessage(err: unknown): string {
    return err instanceof ApiError && err.status < 500
      ? err.message
      : 'Something went wrong. Please try again.'
  }

  async function onSubmit() {
    if (!canSubmit.value || !props.channel) return

    error.value = ''
    formErrors.value = new Map()
    submitting.value = true

    try {
      const updatedChannel = await ChatChannelService.updateChatChannel({
        id: props.channel.id,
        name: form.name.trim(),
      })
      form.name = updatedChannel.name
      emit('success', updatedChannel)
    } catch (err) {
      error.value = errorMessage(err)

      if (err instanceof ValidationError && err.status == 400) {
        error.value = 'Please review the form.'
        formErrors.value = getValidationErrors(err)
      }
    } finally {
      submitting.value = false
    }
  }

  // Restores the name of the channel and removes the errors
  function cleanForm() {
    error.value = ''
    formErrors.value = new Map()
    form.name = props.channel?.name ?? ''
    submitting.value = false
  }

  async function deleteChannel() {
    if (!canDelete.value || !props.channel) return

    error.value = ''
    formErrors.value = new Map()
    submitting.value = true

    try {
      const id = await ChatChannelService.deleteChatChannel(props.channel.id)
      emit('deleted', id)
    } catch (err) {
      error.value = errorMessage(err)
    } finally {
      submitting.value = false
    }
  }
</script>

<template>
  <form class="custom-form" id="update-channel-form" novalidate @submit.prevent="onSubmit">
    <p v-if="error" class="auth-error" role="alert">{{ error }}</p>
    <p class="custom-form-row text-xl font-semibold" :aria-label="props.server?.name">
      Server: {{ props.server?.name }}
    </p>
    <div class="custom-form-row">
      <div class="custom-form-label-wrapper">
        <label for="update-channel-name">Channel Name *</label>
      </div>
      <div class="custom-form-input-wrapper">
        <input
          id="update-channel-name"
          class="custom-form-text-input"
          v-model="form.name"
          type="text"
          required
          placeholder="Channel name"
          minlength="3"
          maxlength="50"
          autocomplete="off"
        />
      </div>

      <p v-for="err in formErrors.get('Name')?.values()" :key="err" class="custom-form-field-error">
        {{ err }}
      </p>
    </div>

    <button class="button-action" type="submit" :disabled="!canSubmit">Update Channel</button>

    <button
      class="button-action button-action-danger"
      type="button"
      :disabled="!canDelete"
      @click="deleteChannel"
    >
      Delete Channel
    </button>
  </form>
</template>
