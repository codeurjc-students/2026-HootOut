<script lang="ts" setup>
import ChatServerService from '@/services/chatServer/ChatServerService'
import { getValidationErrors } from '@/services/common/utils'
import { ApiError, ValidationError, type ChatServer } from '@/types'
import { computed, reactive, ref } from 'vue'

defineExpose({
    cleanForm
})

const emit = defineEmits<{
    (e: 'success', server: ChatServer): void
}>()

const form = reactive({ name: '', description: '' })
const submitting = ref(false)
const error = ref('')

const canSubmit = computed(
    () =>
        !submitting.value &&
        form.name.trim() !== ''
)

const formErrors = ref(new Map<string, Map<string, string>>())

async function onSubmit() {
    if (!canSubmit.value) return

    error.value = ''
    formErrors.value = new Map();
    submitting.value = true

    try {
        const newServer = await ChatServerService.createNewServer({ name: form.name, description: form.description });
        form.name = ''
        form.description = ''
        emit('success', newServer);
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
    form.description = '';
    submitting.value = false;
}

</script>

<template>
    <form class="custom-form" id="create-server-form" novalidate @submit.prevent="onSubmit">

        <p v-if="error" class="auth-error" role="alert">{{ error }}</p>

        <div class="custom-form-row">
            <div class="custom-form-label-wrapper">
                <label for="server-name">Server Name *</label>
            </div>
            <div class="custom-form-input-wrapper">
                <input id="server-name" class="custom-form-text-input" v-model="form.name" type="text" required
                    placeholder="Server name" minlength="5" maxlength="50" />
            </div>

            <p v-for="err in formErrors.get('Name')?.values()" :key="err" class="custom-form-field-error">
                {{ err }}
            </p>

        </div>
        <div class="custom-form-row">
            <div class="custom-form-label-wrapper">
                <label for="server-description">Server description</label>
            </div>
            <div class="custom-form-input-wrapper">
                <input id="server-description" class="custom-form-text-input" type="text" v-model="form.description"
                    placeholder="Short description of the server" maxlength="200" />
            </div>

            <p v-for="err in formErrors.get('Description')?.values()" :key="err" class="custom-form-field-error">
                {{ err }}
            </p>
        </div>

        <button class="button-action" type="submit" :disabled="submitting || !form.name">
            Create Server
        </button>
    </form>
</template>