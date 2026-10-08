<script setup lang="ts">
import { useAuthStore } from '@/stores/auth';
import { ApiError } from '@/types';
import { computed, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

const auth = useAuthStore()
const router = useRouter()

const form = reactive({ username: '', email: '', password: '', confirmPassword: '' })
const submitting = ref(false)
const error = ref('')

const passwordsMatch = computed(() => form.password === form.confirmPassword)
const canSubmit = computed(
    () =>
        !submitting.value &&
        form.username.trim() !== '' &&
        form.email.trim() !== '' &&
        form.password !== '' &&
        passwordsMatch.value,
)

async function onSubmit() {
    if (!canSubmit.value) return

    error.value = ''
    submitting.value = true

    try {
        await auth.register(form.username.trim(), form.email.trim(), form.password)
        await router.push({ name: 'chat' }) // registering also signs the user in
    } catch (err) {
        // 4xx errors carry a message the user can act on (e.g. "username already taken").
        // Adjust this to the error format your API returns.
        error.value =
            err instanceof ApiError && err.status < 500
                ? err.message
                : 'Something went wrong. Please try again.'
    } finally {
        form.password = ''
        form.confirmPassword = ''
        submitting.value = false
    }
}

</script>

<template>
    <section id="register">
        <div class="custom-form-wrapper">
            <h1> Create Account </h1>
            <form class="custom-form" id="register-form" novalidate @submit.prevent="onSubmit">

                <p v-if="error" class="auth-error" role="alert">{{ error }}</p>

                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="username">Username</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input id="username" class="custom-form-text-input" v-model="form.username" type="text"
                            autocomplete="username" autofocus required />
                    </div>
                </div>
                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="email">Email</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input id="email" class="custom-form-text-input" v-model="form.email" type="email"
                            autocomplete="email" required />
                    </div>
                </div>
                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="password">Password</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input id="password" class="custom-form-text-input" type="password" v-model="form.password"
                            autocomplete="new-password" required />
                    </div>
                </div>

                <div class="custom-form-row">
                    <div class="custom-form-label-wrapper">
                        <label for="confirm-password">Confirm Password</label>
                    </div>
                    <div class="custom-form-input-wrapper">
                        <input class="custom-form-text-input" id="confirm-password" type="password"
                            v-model="form.confirmPassword" autocomplete="new-password"
                            :aria-invalid="form.confirmPassword !== '' && !passwordsMatch" required />
                    </div>
                    <p v-if="form.confirmPassword !== '' && !passwordsMatch" class="custom-form-field-error">
                        The passwords don't match.
                    </p>
                </div>

                <button class="button-home" type="submit" :disabled="!canSubmit">
                    {{ submitting ? 'Creating Account' : 'Create Account' }}
                </button>
            </form>
            <div class="custom-form-link">
                <RouterLink :to="{ name: 'login' }">Log In</RouterLink>
            </div>
        </div>
    </section>
</template>

<style>
#register {
    background-color: var(--color-background-soft);
    width: 100%;
    height: 100%;
    padding-top: 5rem;
}

@media(max-width: 520px) {
    #register {
        padding: 0;
    }
}
</style>