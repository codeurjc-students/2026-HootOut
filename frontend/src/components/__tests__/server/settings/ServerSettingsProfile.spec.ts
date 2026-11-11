import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ServerSettingsProfile from '@/components/server/settings/ServerSettingsProfile.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import { dateTimeMapper } from '@/services/common/utils'
import type { ChatServer } from '@/types'

// ---------- helpers ----------

const server = {
  id: 's1',
  name: 'My Server',
  description: 'A server to talk about Vue',
  createdAt: '2026-11-05T10:42:00Z',
} as ChatServer

let pinia: Pinia

// Pass null to mount it without a current server
function mountProfile(currentServer: ChatServer | null = server) {
  if (currentServer) serverInfoStore().setCurrentServerSettings(currentServer)
  return mount(ServerSettingsProfile, {
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
  })
}

// Value shown under each label of the details list, e.g. { 'Server Name:': 'My Server' }
function details(wrapper: VueWrapper): Record<string, string> {
  return Object.fromEntries(
    wrapper.findAll('.border-bottom-1').map((row) => {
      const [label, value] = row.findAll('div')
      return [label!.text(), value!.text()]
    })
  )
}

// ---------- tests ----------

describe('ServerSettingsProfile.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('header', () => {
    it('links back to the server settings', () => {
      const wrapper = mountProfile()

      const link = wrapper.findComponent(RouterLinkStub)
      expect(link.props('to')).toEqual({ name: 'serverSettings' })
      expect(link.text()).toContain('Server Settings:')
    })

    it('shows the page title', () => {
      const wrapper = mountProfile()

      expect(wrapper.find('h1').text()).toBe('Server Settings:')
    })
  })

  describe('server details', () => {
    it('shows the name, description and creation date of the current server', () => {
      const wrapper = mountProfile()

      expect(details(wrapper)).toEqual({
        'Server Name:': 'My Server',
        'Server Description:': 'A server to talk about Vue',
        'Server Created:': dateTimeMapper('2026-11-05T10:42:00Z'),
      })
    })

    it('shows the creation date formatted, not the raw value', () => {
      const wrapper = mountProfile()

      expect(details(wrapper)['Server Created:']).not.toContain('2026-11-05T')
    })

    it('does not show the creation date when the server has none', () => {
      const wrapper = mountProfile({ ...server, createdAt: null })

      expect(details(wrapper)).not.toHaveProperty('Server Created:')
      expect(wrapper.text()).not.toContain('Server Created:')
    })

    it.each([
      ['has no description', undefined],
      ['has an empty description', ''],
    ])('shows "No description" when the server %s', (_, description) => {
      const wrapper = mountProfile({ ...server, description })

      expect(details(wrapper)['Server Description:']).toBe('No description')
    })

    it('updates when the current server changes', async () => {
      const wrapper = mountProfile()

      serverInfoStore().setCurrentServerSettings({ ...server, id: 's2', name: 'Other Server' })
      await wrapper.vm.$nextTick()

      expect(details(wrapper)['Server Name:']).toBe('Other Server')
    })
  })

  describe('without a current server', () => {
    it('shows only the header, without details', () => {
      const wrapper = mountProfile(null)

      expect(details(wrapper)).toEqual({})
      expect(wrapper.text()).not.toContain('Server Name:')
      expect(wrapper.find('h1').text()).toBe('Server Settings:')
      expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(true)
    })
  })
})
