import { describe, it, expect, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { watch, nextTick } from 'vue'
import { userInfoStore } from '@/stores/userInfo'
import type { UserInfo } from '@/types'

const juan = { uid: '1', username: 'juan', email: 'juan@test.com' } as UserInfo
const ana = { uid: '2', username: 'ana', email: 'ana@test.com' } as UserInfo

describe('userInfoStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts with an empty user', () => {
    const store = userInfoStore()

    expect(store.user).toEqual({})
  })

  it('stores the user given to setUser', () => {
    const store = userInfoStore()

    store.setUser(juan)

    expect(store.user).toEqual(juan)
  })

  it('replaces the previous user completely', () => {
    const store = userInfoStore()
    store.setUser(juan)

    store.setUser({ uid: '2', username: 'ana' } as UserInfo)

    // no field of the previous user is kept
    expect(store.user).toEqual({ uid: '2', username: 'ana' })
  })

  it('can be reset by setting an empty user', () => {
    const store = userInfoStore()
    store.setUser(juan)

    store.setUser({} as UserInfo)

    expect(store.user).toEqual({})
  })

  it('shares the same state between all the places that use the store', () => {
    const first = userInfoStore()
    const second = userInfoStore()

    first.setUser(ana)

    expect(second.user).toEqual(ana)
  })

  it('notifies the watchers when the user changes', async () => {
    const store = userInfoStore()
    const usernames: (string | undefined)[] = []
    watch(
      () => store.user.username,
      (username) => usernames.push(username)
    )

    store.setUser(juan)
    await nextTick()
    store.setUser(ana)
    await nextTick()

    expect(usernames).toEqual(['juan', 'ana'])
  })

  it('can be changed directly through the user property', () => {
    const store = userInfoStore()
    store.setUser(juan)

    store.user.username = 'juan-renamed'

    expect(store.user.username).toBe('juan-renamed')
  })

  it('starts empty again in a new pinia instance', () => {
    userInfoStore().setUser(juan)

    setActivePinia(createPinia())

    expect(userInfoStore().user).toEqual({})
  })
})
