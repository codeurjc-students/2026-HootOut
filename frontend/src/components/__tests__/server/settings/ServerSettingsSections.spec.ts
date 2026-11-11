import { describe, it, expect, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter, RouterView, type Router } from 'vue-router'
import ServerSettingsSections from '@/components/server/settings/ServerSettingsSections.vue'

// ---------- router ----------

const placeholder = defineComponent({ render: () => h('p') })

// Same structure as the server settings routes of the app, with placeholder views
// and without guards
function createTestRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/settings/server/:serverId',
        name: 'serverSettings',
        component: defineComponent({ render: () => h(RouterView) }),
        children: [
          { path: 'profile', name: 'serverProfile', component: placeholder },
          {
            path: 'members',
            name: 'serverMembers',
            component: defineComponent({ render: () => h(RouterView) }),
            children: [
              { path: 'active', name: 'serverActiveMembers', component: placeholder },
              { path: 'invitations', name: 'serverInvitations', component: placeholder },
            ],
          },
        ],
      },
    ],
  })
}

// ---------- helpers ----------

const SETTINGS = '/settings/server/s1'

// ServerName is replaced: this test only checks that the menu shows it
const ServerNameStub = defineComponent({
  name: 'ServerName',
  render: () => h('div', { class: 'server-name-stub' }),
})

let router: Router

async function mountAt(path: string) {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(ServerSettingsSections, {
    global: { plugins: [router], stubs: { ServerName: ServerNameStub } },
  })
  await flushPromises()
  return wrapper
}

const link = (wrapper: VueWrapper, text: string) =>
  wrapper.findAll('a').find((a) => a.text() === text)!
const isHighlighted = (wrapper: VueWrapper, text: string) =>
  link(wrapper, text).classes().includes('background-light')

// ---------- tests ----------

describe('ServerSettingsMenu.vue', () => {
  beforeEach(() => {
    router = createTestRouter()
  })

  it('shows the server name', async () => {
    const wrapper = await mountAt(SETTINGS)

    expect(wrapper.find('.server-name-stub').exists()).toBe(true)
  })

  it('links to the profile and the members of the same server', async () => {
    const wrapper = await mountAt(SETTINGS)

    expect(link(wrapper, 'Server Profile').attributes('href')).toBe(`${SETTINGS}/profile`)
    expect(link(wrapper, 'Server Members').attributes('href')).toBe(`${SETTINGS}/members`)
  })

  it('does not highlight any link on the settings page itself', async () => {
    const wrapper = await mountAt(SETTINGS)

    expect(isHighlighted(wrapper, 'Server Profile')).toBe(false)
    expect(isHighlighted(wrapper, 'Server Members')).toBe(false)
  })

  it('highlights only the profile link on the profile page', async () => {
    const wrapper = await mountAt(`${SETTINGS}/profile`)

    expect(isHighlighted(wrapper, 'Server Profile')).toBe(true)
    expect(isHighlighted(wrapper, 'Server Members')).toBe(false)
  })

  it.each(['members', 'members/active', 'members/invitations'])(
    'highlights only the members link on %s',
    async (path) => {
      const wrapper = await mountAt(`${SETTINGS}/${path}`)

      expect(isHighlighted(wrapper, 'Server Members')).toBe(true)
      expect(isHighlighted(wrapper, 'Server Profile')).toBe(false)
    }
  )

  it('moves the highlight when navigating', async () => {
    const wrapper = await mountAt(`${SETTINGS}/profile`)

    await router.push(`${SETTINGS}/members/invitations`)
    await flushPromises()

    expect(isHighlighted(wrapper, 'Server Members')).toBe(true)
    expect(isHighlighted(wrapper, 'Server Profile')).toBe(false)
  })

  it('navigates to the page of a link when it is clicked', async () => {
    const wrapper = await mountAt(`${SETTINGS}/profile`)

    await link(wrapper, 'Server Members').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('serverMembers')
    expect(router.currentRoute.value.params.serverId).toBe('s1')
  })
})
