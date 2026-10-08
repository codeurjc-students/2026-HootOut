import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { post } from '@/services/common/api/ApiService'
import { ApiError, type AuthResponse } from '@/types'

// Serialises refreshes across browser tabs: refresh tokens are single use, and two tabs
// refreshing at the same moment would look like a stolen token to the server.
async function withRefreshLock(fn: () => Promise<boolean>): Promise<boolean> {
    // navigator.locks only exists in secure contexts (https, or http://localhost).
    if (!navigator.locks) return fn()
    return await navigator.locks.request('auth-refresh', fn)
}

export const useAuthStore = defineStore('auth', () => {
    // Memory only. Never persist this (no localStorage, no pinia-plugin-persistedstate).
    const accessToken = ref<string | null>(null)

    // False until the first attempt to restore the session (from the refresh cookie) has finished.
    const ready = ref(false)

    const isAuthenticated = computed(() => accessToken.value !== null)

    let inFlight: Promise<boolean> | null = null

    async function login(email: string, password: string): Promise<void> {
        const auth = await post<AuthResponse>('auth/login', { email, password })
        accessToken.value = auth.accessToken
    }

    async function register(username: string, email: string, password: string): Promise<void> {
        // Registering also signs the user in, so the response carries tokens like a login.
        const auth = await post<AuthResponse>('auth/register', { username, email, password })
        accessToken.value = auth.accessToken
    }

    async function logout(): Promise<void> {
        try {
            await post('auth/logout') // revokes the session and clears the cookie
        } catch {
            // Even if the server can't be reached, drop the local state.
        } finally {
            clear()
        }
    }

    // Trades the refresh cookie for a new access token. Parallel callers share one request.
    function refresh(): Promise<boolean> {
        inFlight ??= withRefreshLock(async () => {
            try {
                const auth = await post<AuthResponse>('auth/refresh')
                accessToken.value = auth.accessToken
                return true
            } catch (err) {
                if (err instanceof ApiError) accessToken.value = null  // the server rejected the session
                return false                                           // network error: keep state
            }
        }).finally(() => { inFlight = null })

        return inFlight
    }

    // Called once by the router guard on the first navigation.
    async function init(): Promise<void> {
        if (ready.value) return
        await refresh()
        ready.value = true
    }

    function clear(): void {
        accessToken.value = null
    }

    return { accessToken, ready, isAuthenticated, login, register, logout, refresh, init, clear }
})
