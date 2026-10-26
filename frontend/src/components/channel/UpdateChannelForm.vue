<script setup lang="ts">
import ChatChannelService from '@/services/chatChannel/ChatChannelService';
import { getValidationErrors } from '@/services/common/utils';
import { ApiError, ValidationError, type ChatChannel } from '@/types';
import { computed, onMounted, reactive, ref } from 'vue';

defineExpose({
    cleanForm
})

const props = defineProps({
    channel: Object,
    server: Object
})

const emit = defineEmits<{
    (e: 'success', channel: ChatChannel): void,
    (e: 'deleted', id: string): void
}>()

const form = reactive({ name: props.channel?.name })
const submitting = ref(false)
const error = ref('')

const firstinput = ref<HTMLElement>();
onMounted(() => firstinput?.value?.focus())

const canSubmit = computed(
    () =>
        !submitting.value &&
        form.name.trim() !== '' &&
        props.channel?.Id?.trim() !== ''
)

const formErrors = ref(new Map<string, Map<string, string>>())

async function onSubmit() {
    if (!canSubmit.value) return

    error.value = ''
    formErrors.value = new Map();
    submitting.value = true

    try {
        const updatedChannel = await ChatChannelService.updateChatChannel({ id: props.channel?.id, name: form.name });
        form.name = ''
        emit('success', updatedChannel);
    } catch (err) {
        error.value = err instanceof ApiError && err.status < 500
            ? err.message
            : 'Something went wrong. Please try again.'

        if (err instanceof ValidationError && err.status == 400) {
            error.value = 'Please review the form.'
            formErrors.value = getValidationErrors(err);
        }
    } finally {
        submitting.value = false
    }
}

async function cleanForm() {
    formErrors.value = new Map();
    form.name = '';
    submitting.value = false;
}

async function deleteChannel() {
    try {
        const id = await ChatChannelService.deleteChatChannel(props.channel?.id);
        emit('deleted', id)
    } catch (err) {
        error.value = err instanceof ApiError && err.status < 500
            ? err.message
            : 'Something went wrong. Please try again.'
    } finally {
        submitting.value = false
    }
}

</script>

<template>

    <form class="custom-form" id="update-channel-form" novalidate @submit.prevent="onSubmit">
        <p class="custom-form-row"> Server: {{ props.server?.name }}</p>
        <p v-if="error" class="auth-error" role="alert">{{ error }}</p>
        <div class="custom-form-row">
            <div class="custom-form-label-wrapper">
                <label for="server-name">Channel Name *</label>
            </div>
            <div class="custom-form-input-wrapper">
                <input id="server-name" ref="firstinput" class="custom-form-text-input" v-model="form.name" type="text"
                    required placeholder="Server name" minlength="3" maxlength="50" autofocus autocomplete="off" />
            </div>

            <p v-for="err in formErrors.get('Name')?.values()" :key="err" class="custom-form-field-error">
                {{ err }}
            </p>

        </div>

        <button class="button-action" type="submit" :disabled="submitting || !form.name">
            Update Channel
        </button>

        <button class="button-action button-action-danger" type="button" :disabled="submitting || !form.name"
            @click="deleteChannel">
            Delete Channel
        </button>
    </form>


</template>