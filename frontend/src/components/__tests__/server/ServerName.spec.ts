import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, nextTick } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ServerName from '@/components/server/ServerName.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import type { ChatServer } from '@/types'

// ---------- stubs ----------

// Modal replaced by a stub that exposes open() and close() spies,
// so the test checks when ServerName opens and closes it
const modal = { open: vi.fn(), close: vi.fn() }
const ModalStub = defineComponent({
  name: 'Modal',
  props: { title: { type: String, required: true } },
  setup(props, { expose, slots }) {
    expose(modal)
    return () => h('div', { class: 'modal-stub' }, [h('h3', props.title), slots.body?.()])
  },
})

// InviteMemberForm replaced by a stub: the test only checks the server it gets
// and what happens when it emits success
const InviteMemberFormStub = defineComponent({
  name: 'InviteMemberForm',
  props: { server: { type: Object, required: true } },
  emits: ['success'],
  setup: () => () => h('form', { class: 'invite-form-stub' }),
})

// ---------- helpers ----------

const server = { id: 's1', name: 'My Server' } as ChatServer

let pinia: Pinia

// Pass null to mount it without a current server
function mountServerName(currentServer: ChatServer | null = server) {
  if (currentServer) serverInfoStore().setCurrentServerSettings(currentServer)
  return mount(ServerName, {
    global: {
      plugins: [pinia],
      stubs: {
        RouterLink: RouterLinkStub,
        Modal: ModalStub,
        InviteMemberForm: InviteMemberFormStub,
      },
    },
  })
}

const inviteButton = (wrapper: VueWrapper) => wrapper.find('button[aria-label="Invite member"]')
const settingsLink = (wrapper: VueWrapper) => wrapper.findComponent(RouterLinkStub)

// ---------- tests ----------

describe('ServerName.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    modal.open.mockClear()
    modal.close.mockClear()
  })

  it('shows the name of the current server', () => {
    const wrapper = mountServerName()

    expect(wrapper.find('.server-name > div').text()).toBe('My Server')
  })

  describe('invite member', () => {
    it('has a labelled button to invite members', () => {
      const wrapper = mountServerName()

      expect(inviteButton(wrapper).exists()).toBe(true)
    })

    it('does not open the modal on mount', () => {
      mountServerName()

      expect(modal.open).not.toHaveBeenCalled()
    })

    it('opens the invite modal when the button is clicked', async () => {
      const wrapper = mountServerName()

      await inviteButton(wrapper).trigger('click')

      expect(modal.open).toHaveBeenCalledTimes(1)
      expect(modal.close).not.toHaveBeenCalled()
    })

    it('shows the invite form in a modal titled "Invite Member"', () => {
      const wrapper = mountServerName()

      expect(wrapper.findComponent(ModalStub).props('title')).toBe('Invite Member')
      expect(wrapper.findComponent(ModalStub).findComponent(InviteMemberFormStub).exists()).toBe(
        true
      )
    })

    it('passes the current server to the invite form', () => {
      const wrapper = mountServerName()

      expect(wrapper.findComponent(InviteMemberFormStub).props('server')).toEqual(server)
    })

    it('closes the modal when the invitation is sent', async () => {
      const wrapper = mountServerName()
      await inviteButton(wrapper).trigger('click')

      wrapper.findComponent(InviteMemberFormStub).vm.$emit('success')

      expect(modal.close).toHaveBeenCalledTimes(1)
    })
  })

  describe('server settings link', () => {
    it('links to the profile settings of the current server', () => {
      const wrapper = mountServerName()

      expect(settingsLink(wrapper).props('to')).toEqual({
        name: 'serverProfile',
        params: { serverId: 's1' },
      })
      expect(settingsLink(wrapper).attributes('aria-label')).toBe('Server settings')
    })

    it('links to the new server when the current server changes', async () => {
      const wrapper = mountServerName()

      serverInfoStore().setCurrentServerSettings({ id: 's2', name: 'Other' } as ChatServer)
      await nextTick()

      expect(settingsLink(wrapper).props('to')).toEqual({
        name: 'serverProfile',
        params: { serverId: 's2' },
      })
      expect(wrapper.findComponent(InviteMemberFormStub).props('server')).toEqual({
        id: 's2',
        name: 'Other',
      })
    })
  })

  describe('without a current server', () => {
    it('renders without errors and without a name', () => {
      const wrapper = mountServerName(null)

      expect(wrapper.find('.server-name > div').text()).toBe('')
    })

    it('does not show the invite button, the settings link or the modal', () => {
      const wrapper = mountServerName(null)

      expect(inviteButton(wrapper).exists()).toBe(false)
      expect(settingsLink(wrapper).exists()).toBe(false)
      expect(wrapper.findComponent(ModalStub).exists()).toBe(false)
    })

    it('shows the actions once there is a current server', async () => {
      const wrapper = mountServerName(null)

      serverInfoStore().setCurrentServerSettings(server)
      await nextTick()

      expect(inviteButton(wrapper).exists()).toBe(true)
      expect(settingsLink(wrapper).props('to')).toEqual({
        name: 'serverProfile',
        params: { serverId: 's1' },
      })
    })
  })
})
