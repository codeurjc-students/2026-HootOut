<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { ApiError } from '@/types'
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'


const auth = useAuthStore()
const router = useRouter()

const form = reactive({ email: '', password: '' })
const submitting = ref(false)
const error = ref('')

const canSubmit = computed(
    () =>
        !submitting.value &&
        form.email.trim() !== '' &&
        form.password !== ''
)

async function onSubmit() {
    if (!canSubmit.value) return

    error.value = ''
    submitting.value = true

    try {
        await auth.login(form.email.trim(), form.password)
        await router.push({ name: 'chat' }) // registering also signs the user in
    } catch (err) {
        // 4xx errors carry a message the user can act on (e.g. "username already taken").
        // Adjust this to the error format your API returns.
        error.value =
            err instanceof ApiError && (err.status === 400 || err.status === 401)
                ? 'Invalid username or password.'
                : 'Something went wrong. Please try again.'
    } finally {
        form.password = ''
        submitting.value = false
    }
}
</script>

<template>
    <section id="login">
        <div class="custom-form-wrapper">
            <h1> Welcome Back! </h1>
            <form class="custom-form" id="login-form" novalidate @submit.prevent="onSubmit">

                <p v-if="error" class="auth-error" role="alert">{{ error }}</p>

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
                            autocomplete="current-password" />
                    </div>
                </div>

                <button class="button-home" type="submit" :disabled="submitting || !form.email || !form.password">
                    Log In
                </button>
            </form>
            <div class="custom-form-link">
                <RouterLink to="/register">Register</RouterLink>
            </div>
        </div>
    </section>
</template>

<style>
#login {
    background-color: var(--color-background-soft);
    width: 100%;
    height: 100%;
    padding-top: 5rem;
}

@media(max-width: 520px) {
    #login {
        padding: 0;
    }
}
</style>