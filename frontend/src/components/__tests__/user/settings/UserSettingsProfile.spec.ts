import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import UserSettingsProfile from '@/components/user/settings/UserSettingsProfile.vue'
import { userInfoStore } from '@/stores/userInfo'
import { dateTimeMapper } from '@/services/common/utils'
import type { UserInfo } from '@/types'

// ---------- helpers ----------

let pinia: Pinia

function mountProfile(user: UserInfo) {
  userInfoStore().setUser(user)
  return mount(UserSettingsProfile, {
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
  })
}

// Text of the value shown below a "Label:" title
function field(wrapper: VueWrapper, label: string) {
  const title = wrapper.findAll('.font-semibold').find((div) => div.text() === label)
  return title?.element.nextElementSibling?.textContent?.trim()
}

// ---------- tests ----------

describe('UserSettingsProfile.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('links back to the user settings', () => {
    const wrapper = mountProfile({ username: 'testuser' })

    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.props('to')).toEqual({ name: 'userSettings' })
    expect(link.text()).toContain('User Profile:')
  })

  it('shows the page title', () => {
    const wrapper = mountProfile({ username: 'testuser' })

    expect(wrapper.find('h1').text()).toBe('User Profile:')
  })

  it('shows the username and the email of the user', () => {
    const wrapper = mountProfile({ username: 'testuser', email: 'testuser@example.com' })

    expect(field(wrapper, 'Username:')).toBe('testuser')
    expect(field(wrapper, 'Email:')).toBe('testuser@example.com')
  })

  it('shows the account creation date in the local format', () => {
    const wrapper = mountProfile({ username: 'testuser', createdAt: '2026-11-05T10:42:00' })

    expect(field(wrapper, 'Account Created:')).toBe(dateTimeMapper('2026-11-05T10:42:00'))
  })

  it.each([
    ['null', null],
    ['undefined', undefined],
  ])('hides the account creation date when it is %s', (_, createdAt) => {
    const wrapper = mountProfile({ username: 'testuser', createdAt })

    expect(wrapper.text()).not.toContain('Account Created:')
  })

  it('updates when the user changes', async () => {
    const wrapper = mountProfile({ username: 'testuser', email: 'testuser@example.com' })

    userInfoStore().setUser({ username: 'other', email: 'other@example.com' })
    await wrapper.vm.$nextTick()

    expect(field(wrapper, 'Username:')).toBe('other')
    expect(field(wrapper, 'Email:')).toBe('other@example.com')
  })
})
