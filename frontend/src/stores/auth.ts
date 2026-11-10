import { computed, ref } from 'vue'
import { defineStore, getActivePinia } from 'pinia'
import { post } from '@/services/common/api/ApiService'
import { ApiError, type AuthResponse } from '@/types'

// Serialises refreshes across browser tabs: refresh tokens are single use, and two tabs
// refreshing at the same moment would look like a stolen token to the server.
async function withRefreshLock(fn: () => Promise<boolean>): Promise<boolean> {
  // navigator.locks only exists in secure contexts (https, or http://localhost).
  if (!navigator.locks) return fn()
  return await navigator.locks.request('auth-refresh', fn)
}

// Responses that mean the server rejected the session. Any other error (5xx, network...)
// is treated as temporary and keeps the current state.
const SESSION_REJECTED_STATUSES = [400, 401, 403]

export const useAuthStore = defineStore('auth', () => {
  // Memory only. Never persist this (no localStorage, no pinia-plugin-persistedstate).
  const accessToken = ref<string | null>(null)

  // False until the first attempt to restore the session (from the refresh cookie) has finished.
  const ready = ref(false)

  const isAuthenticated = computed(() => accessToken.value !== null)

  let inFlight: Promise<boolean> | null = null

  // Incremented on every login, register, logout or clear. A refresh that started with
  // an older version finished after the session changed, so its result is ignored.
  let sessionVersion = 0

  function setSession(token: string | null): void {
    sessionVersion++
    accessToken.value = token
  }

  async function login(email: string, password: string): Promise<void> {
    const auth = await post<AuthResponse>('auth/login', { email, password })
    setSession(auth.accessToken)
  }

  async function register(username: string, email: string, password: string): Promise<void> {
    // Registering also signs the user in, so the response carries tokens like a login.
    const auth = await post<AuthResponse>('auth/register', { username, email, password })
    setSession(auth.accessToken)
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

  async function requestRefresh(version: number): Promise<boolean> {
    try {
      const auth = await post<AuthResponse>('auth/refresh')
      // The session changed while refreshing (e.g. logout): don't overwrite it
      if (version !== sessionVersion) return isAuthenticated.value
      accessToken.value = auth.accessToken
      return true
    } catch (err) {
      if (version !== sessionVersion) return isAuthenticated.value
      if (err instanceof ApiError && SESSION_REJECTED_STATUSES.includes(err.status)) {
        accessToken.value = null // the server rejected the session
      }
      return false // temporary error: keep state
    }
  }

  // Trades the refresh cookie for a new access token. Parallel callers share one request.
  // Returns whether there is a valid session afterwards.
  function refresh(): Promise<boolean> {
    if (!inFlight) {
      const version = sessionVersion
      inFlight = withRefreshLock(() => requestRefresh(version)).finally(() => {
        inFlight = null
      })
    }
    return inFlight
  }

  // Called once by the router guard on the first navigation.
  async function init(): Promise<void> {
    if (ready.value) return
    await refresh()
    ready.value = true
  }

  function clear(): void {
    setSession(null)
    window.location.assign('/')
  }

  return { accessToken, ready, isAuthenticated, login, register, logout, refresh, init, clear }
})
