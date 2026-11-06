import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { ApiError, type AuthResponse } from '@/types'

// ---------- mocks ----------

const api = vi.hoisted(() => ({ post: vi.fn() }))
vi.mock('@/services/common/api/ApiService', () => ({ post: api.post }))

// ---------- helpers ----------

function authResponse(accessToken: string): AuthResponse {
  return { accessToken, tokenType: 'Bearer' }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

// Leaves the next refresh request pending, so the test decides when and how it finishes
function pendingRefresh() {
  const request = deferred<AuthResponse>()
  api.post.mockImplementationOnce((url: string) => {
    if (url !== 'auth/refresh') throw new Error(`Unexpected request to ${url}`)
    return request.promise
  })
  return request
}

// Fake Web Locks API: runs the callback straight away and records the lock names
function fakeLocks() {
  const held: string[] = []
  const locks = {
    held,
    request: vi.fn(async (name: string, callback: () => Promise<unknown>) => {
      held.push(name)
      try {
        return await callback()
      } finally {
        held.splice(held.indexOf(name), 1)
      }
    }),
  }
  vi.stubGlobal('navigator', { ...navigator, locks })
  return locks
}

async function loggedInStore(token = 'token-login') {
  const store = useAuthStore()
  api.post.mockResolvedValueOnce(authResponse(token))
  await store.login('user@test.com', 'secret')
  api.post.mockReset()
  return store
}

// ---------- tests ----------

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    api.post.mockReset()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  describe('initial state', () => {
    it('starts without a session and not ready', () => {
      const store = useAuthStore()

      expect(store.accessToken).toBeNull()
      expect(store.isAuthenticated).toBe(false)
      expect(store.ready).toBe(false)
    })
  })

  describe('login', () => {
    it('posts the credentials to auth/login', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-1'))
      const store = useAuthStore()

      await store.login('user@test.com', 'secret')

      expect(api.post).toHaveBeenCalledWith('auth/login', {
        email: 'user@test.com',
        password: 'secret',
      })
    })

    it('stores the access token and is authenticated', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-1'))
      const store = useAuthStore()

      await store.login('user@test.com', 'secret')

      expect(store.accessToken).toBe('token-1')
      expect(store.isAuthenticated).toBe(true)
    })

    it('throws the error and stays logged out when the login fails', async () => {
      api.post.mockRejectedValueOnce(new ApiError(401, 'Invalid credentials'))
      const store = useAuthStore()

      await expect(store.login('user@test.com', 'wrong')).rejects.toThrow('Invalid credentials')
      expect(store.isAuthenticated).toBe(false)
    })
  })

  describe('register', () => {
    it('posts the new user to auth/register', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-1'))
      const store = useAuthStore()

      await store.register('juan', 'juan@test.com', 'secret')

      expect(api.post).toHaveBeenCalledWith('auth/register', {
        username: 'juan',
        email: 'juan@test.com',
        password: 'secret',
      })
    })

    it('signs the user in with the returned token', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-1'))
      const store = useAuthStore()

      await store.register('juan', 'juan@test.com', 'secret')

      expect(store.accessToken).toBe('token-1')
      expect(store.isAuthenticated).toBe(true)
    })

    it('throws the error and stays logged out when the registration fails', async () => {
      api.post.mockRejectedValueOnce(new ApiError(409, 'Email already in use'))
      const store = useAuthStore()

      await expect(store.register('juan', 'juan@test.com', 'secret')).rejects.toThrow(
        'Email already in use'
      )
      expect(store.isAuthenticated).toBe(false)
    })
  })

  describe('logout', () => {
    it('posts to auth/logout', async () => {
      const store = await loggedInStore()
      api.post.mockResolvedValueOnce(undefined)

      await store.logout()

      expect(api.post).toHaveBeenCalledWith('auth/logout')
    })

    it('removes the session', async () => {
      const store = await loggedInStore()
      api.post.mockResolvedValueOnce(undefined)

      await store.logout()

      expect(store.accessToken).toBeNull()
      expect(store.isAuthenticated).toBe(false)
    })

    it.each([
      ['the server rejects it', new ApiError(500, 'Server error')],
      ['the server cannot be reached', new TypeError('Failed to fetch')],
    ])('removes the session without throwing when %s', async (_, error) => {
      const store = await loggedInStore()
      api.post.mockRejectedValueOnce(error)

      await expect(store.logout()).resolves.toBeUndefined()
      expect(store.isAuthenticated).toBe(false)
    })
  })

  describe('clear', () => {
    it('removes the session without calling the server', async () => {
      const store = await loggedInStore()

      store.clear()

      expect(store.isAuthenticated).toBe(false)
      expect(api.post).not.toHaveBeenCalled()
    })
  })

  describe('refresh', () => {
    it('posts to auth/refresh, stores the new token and returns true', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-refreshed'))
      const store = useAuthStore()

      const result = await store.refresh()

      expect(api.post).toHaveBeenCalledWith('auth/refresh')
      expect(result).toBe(true)
      expect(store.accessToken).toBe('token-refreshed')
    })

    it('replaces the current token', async () => {
      const store = await loggedInStore('token-old')
      api.post.mockResolvedValueOnce(authResponse('token-new'))

      await store.refresh()

      expect(store.accessToken).toBe('token-new')
    })

    it.each([401, 403])(
      'removes the session and returns false when the server answers %i',
      async (status) => {
        const store = await loggedInStore()
        api.post.mockRejectedValueOnce(new ApiError(status, 'Session expired'))

        const result = await store.refresh()

        expect(result).toBe(false)
        expect(store.isAuthenticated).toBe(false)
      }
    )

    it.each([500, 502, 503])(
      'keeps the session and returns false when the server answers %i',
      async (status) => {
        const store = await loggedInStore('token-login')
        api.post.mockRejectedValueOnce(new ApiError(status, 'Temporary error'))

        const result = await store.refresh()

        expect(result).toBe(false)
        expect(store.accessToken).toBe('token-login')
      }
    )

    it('keeps the session and returns false when the server cannot be reached', async () => {
      const store = await loggedInStore('token-login')
      api.post.mockRejectedValueOnce(new TypeError('Failed to fetch'))

      const result = await store.refresh()

      expect(result).toBe(false)
      expect(store.accessToken).toBe('token-login')
    })

    it('shares a single request between parallel calls', async () => {
      const request = pendingRefresh()
      const store = useAuthStore()

      const first = store.refresh()
      const second = store.refresh()
      request.resolve(authResponse('token-refreshed'))

      expect(await first).toBe(true)
      expect(await second).toBe(true)
      expect(api.post).toHaveBeenCalledTimes(1)
    })

    it('makes a new request once the previous one has finished', async () => {
      api.post
        .mockResolvedValueOnce(authResponse('token-1'))
        .mockResolvedValueOnce(authResponse('token-2'))
      const store = useAuthStore()

      await store.refresh()
      await store.refresh()

      expect(api.post).toHaveBeenCalledTimes(2)
      expect(store.accessToken).toBe('token-2')
    })

    it('makes a new request after a failed one', async () => {
      api.post
        .mockRejectedValueOnce(new TypeError('Failed to fetch'))
        .mockResolvedValueOnce(authResponse('token-1'))
      const store = useAuthStore()

      expect(await store.refresh()).toBe(false)
      expect(await store.refresh()).toBe(true)
      expect(api.post).toHaveBeenCalledTimes(2)
    })
  })

  describe('refresh finishing after the session changed', () => {
    it('stays logged out when logout happens while refreshing', async () => {
      const store = await loggedInStore()
      const request = pendingRefresh()
      api.post.mockResolvedValueOnce(undefined) // logout

      const refreshing = store.refresh()
      await store.logout()
      request.resolve(authResponse('token-refreshed'))

      expect(await refreshing).toBe(false)
      expect(store.isAuthenticated).toBe(false)
    })

    it('stays logged out when the session is cleared while refreshing', async () => {
      const store = await loggedInStore()
      const request = pendingRefresh()

      const refreshing = store.refresh()
      store.clear()
      request.resolve(authResponse('token-refreshed'))
      await refreshing

      expect(store.isAuthenticated).toBe(false)
    })

    it('keeps the new login when the refresh is rejected after it', async () => {
      const store = useAuthStore()
      const request = pendingRefresh()
      api.post.mockResolvedValueOnce(authResponse('token-login')) // login

      const refreshing = store.refresh()
      await store.login('user@test.com', 'secret')
      request.reject(new ApiError(401, 'Session expired'))

      expect(await refreshing).toBe(true)
      expect(store.accessToken).toBe('token-login')
    })

    it('keeps the token of the new login instead of the refreshed one', async () => {
      const store = useAuthStore()
      const request = pendingRefresh()
      api.post.mockResolvedValueOnce(authResponse('token-login')) // login

      const refreshing = store.refresh()
      await store.login('user@test.com', 'secret')
      request.resolve(authResponse('token-refreshed'))
      await refreshing

      expect(store.accessToken).toBe('token-login')
    })

    it('keeps the token of a registration made while refreshing', async () => {
      const store = useAuthStore()
      const request = pendingRefresh()
      api.post.mockResolvedValueOnce(authResponse('token-register')) // register

      const refreshing = store.refresh()
      await store.register('juan', 'juan@test.com', 'secret')
      request.reject(new ApiError(401, 'Session expired'))
      await refreshing

      expect(store.accessToken).toBe('token-register')
    })

    it('applies the result of a refresh started after the session changed', async () => {
      const store = await loggedInStore('token-login')
      api.post.mockResolvedValueOnce(authResponse('token-refreshed'))

      await store.refresh()

      expect(store.accessToken).toBe('token-refreshed')
    })
  })

  describe('refresh lock between tabs', () => {
    it('refreshes without a lock when navigator.locks is not available', async () => {
      vi.stubGlobal('navigator', { ...navigator, locks: undefined })
      api.post.mockResolvedValueOnce(authResponse('token-refreshed'))
      const store = useAuthStore()

      expect(await store.refresh()).toBe(true)
    })

    it('makes the request while holding the "auth-refresh" lock', async () => {
      const locks = fakeLocks()
      let heldDuringRequest: string[] = []
      api.post.mockImplementationOnce(async () => {
        heldDuringRequest = [...locks.held]
        return authResponse('token-refreshed')
      })
      const store = useAuthStore()

      await store.refresh()

      expect(locks.request).toHaveBeenCalledWith('auth-refresh', expect.any(Function))
      expect(heldDuringRequest).toEqual(['auth-refresh'])
      expect(locks.held).toEqual([])
    })

    it('returns the result of the refresh made inside the lock', async () => {
      fakeLocks()
      api.post.mockRejectedValueOnce(new ApiError(401, 'Session expired'))
      const store = useAuthStore()

      expect(await store.refresh()).toBe(false)
    })

    it('requests the lock once for parallel calls', async () => {
      const locks = fakeLocks()
      api.post.mockResolvedValueOnce(authResponse('token-refreshed'))
      const store = useAuthStore()

      await Promise.all([store.refresh(), store.refresh(), store.refresh()])

      expect(locks.request).toHaveBeenCalledTimes(1)
    })
  })

  describe('init', () => {
    it('restores the session from the refresh cookie and becomes ready', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-refreshed'))
      const store = useAuthStore()

      await store.init()

      expect(api.post).toHaveBeenCalledWith('auth/refresh')
      expect(store.isAuthenticated).toBe(true)
      expect(store.ready).toBe(true)
    })

    it('becomes ready without a session when there is no valid cookie', async () => {
      api.post.mockRejectedValueOnce(new ApiError(401, 'No session'))
      const store = useAuthStore()

      await store.init()

      expect(store.isAuthenticated).toBe(false)
      expect(store.ready).toBe(true)
    })

    it('becomes ready when the server cannot be reached', async () => {
      api.post.mockRejectedValueOnce(new TypeError('Failed to fetch'))
      const store = useAuthStore()

      await store.init()

      expect(store.ready).toBe(true)
    })

    it('is not ready until the refresh has finished', async () => {
      const request = pendingRefresh()
      const store = useAuthStore()

      const initializing = store.init()
      expect(store.ready).toBe(false)

      request.resolve(authResponse('token-refreshed'))
      await initializing
      expect(store.ready).toBe(true)
    })

    it('does not refresh again once it is ready', async () => {
      api.post.mockResolvedValueOnce(authResponse('token-refreshed'))
      const store = useAuthStore()

      await store.init()
      await store.init()

      expect(api.post).toHaveBeenCalledTimes(1)
    })

    it('shares the request between parallel calls', async () => {
      const request = pendingRefresh()
      const store = useAuthStore()

      const first = store.init()
      const second = store.init()
      request.resolve(authResponse('token-refreshed'))
      await Promise.all([first, second])

      expect(api.post).toHaveBeenCalledTimes(1)
      expect(store.ready).toBe(true)
    })
  })
})
