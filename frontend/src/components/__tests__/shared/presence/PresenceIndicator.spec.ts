import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import PresenceIndicator from '@/components/shared/presence/PresenceIndicator.vue'
import { presenceStore } from '@/stores/presence'
import { PresenceStatus, WSMessageType } from '@/types'

let pinia: Pinia

function mountIndicator(userId?: string | null) {
  return mount(PresenceIndicator, { props: { userId }, global: { plugins: [pinia] } })
}

function receivePresence(userId: string, status: PresenceStatus) {
  presenceStore().handleMessage({
    type: WSMessageType.Presence,
    channel: `presence.user.${userId}`,
    payload: JSON.stringify({ UserId: userId, Status: status }),
  })
}

describe('PresenceIndicator.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
  })

  it('is offline when nothing is known about the user', () => {
    const wrapper = mountIndicator('u1')

    expect(wrapper.attributes('data-presence')).toBe('offline')
    expect(wrapper.attributes('title')).toBe('Offline')
    expect(wrapper.classes()).toContain('presence-offline')
  })

  it('is online when the user is online', () => {
    receivePresence('u1', PresenceStatus.Online)

    const wrapper = mountIndicator('u1')

    expect(wrapper.attributes('data-presence')).toBe('online')
    expect(wrapper.attributes('title')).toBe('Online')
    expect(wrapper.classes()).toContain('presence-online')
  })

  it('changes when the presence of the user changes', async () => {
    const wrapper = mountIndicator('u1')

    receivePresence('u1', PresenceStatus.Online)
    await nextTick()
    expect(wrapper.attributes('data-presence')).toBe('online')

    receivePresence('u1', PresenceStatus.Offline)
    await nextTick()
    expect(wrapper.attributes('data-presence')).toBe('offline')
  })

  it('does not change with the presence of other users', async () => {
    const wrapper = mountIndicator('u1')

    receivePresence('u2', PresenceStatus.Online)
    await nextTick()

    expect(wrapper.attributes('data-presence')).toBe('offline')
  })

  it('follows the user of the prop when it changes', async () => {
    receivePresence('u2', PresenceStatus.Online)
    const wrapper = mountIndicator('u1')

    await wrapper.setProps({ userId: 'u2' })

    expect(wrapper.attributes('data-presence')).toBe('online')
  })

  it('is offline without a user', () => {
    expect(mountIndicator(undefined).attributes('data-presence')).toBe('offline')
    expect(mountIndicator(null).attributes('data-presence')).toBe('offline')
  })

  it('is hidden from assistive technologies and has no text', () => {
    const wrapper = mountIndicator('u1')

    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.text()).toBe('')
  })
})
