import { describe, it, expect, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter, RouterView, type Router } from 'vue-router'
import UserSettingsSections from '@/components/user/settings/UserSettingsSections.vue'

// ---------- helpers ----------

let router: Router

// Same structure as the user settings routes of the app, with test components
function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  const nested = defineComponent({ render: () => h(RouterView) })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/settings/user',
        name: 'userSettings',
        component: nested,
        children: [
          { path: 'profile', name: 'userSettingsProfile', component: placeholder },
          { path: 'invitations', name: 'userServerInvitations', component: placeholder },
          {
            path: 'friendRequests',
            name: 'friendRequests',
            component: nested,
            children: [
              { path: 'received', name: 'receivedFriendRequests', component: placeholder },
              { path: 'sent', name: 'sentFriendRequests', component: placeholder },
            ],
          },
        ],
      },
    ],
  })
}

async function mountSections(path = '/settings/user') {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(UserSettingsSections, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

const link = (wrapper: VueWrapper, text: string) =>
  wrapper.findAll('a').find((a) => a.text() === text)!
const highlighted = (wrapper: VueWrapper) =>
  wrapper
    .findAll('a')
    .filter((a) => a.classes().includes('background-light'))
    .map((a) => a.text())

// ---------- tests ----------

describe('UserSettingsSections.vue', () => {
  beforeEach(() => {
    router = createTestRouter()
  })

  it('shows the title', async () => {
    const wrapper = await mountSections()

    expect(wrapper.find('h1').text()).toBe('User Settings')
  })

  it('links to each section of the user settings', async () => {
    const wrapper = await mountSections()

    expect(wrapper.findAll('a').map((a) => a.text())).toEqual([
      'Profile',
      'Server Invitations',
      'Friend Requests',
    ])
    expect(link(wrapper, 'Profile').attributes('href')).toBe('/settings/user/profile')
    expect(link(wrapper, 'Server Invitations').attributes('href')).toBe(
      '/settings/user/invitations'
    )
    expect(link(wrapper, 'Friend Requests').attributes('href')).toBe(
      '/settings/user/friendRequests'
    )
  })

  it('highlights no section on the user settings page itself', async () => {
    const wrapper = await mountSections('/settings/user')

    expect(highlighted(wrapper)).toEqual([])
  })

  it.each([
    ['/settings/user/profile', 'Profile'],
    ['/settings/user/invitations', 'Server Invitations'],
    ['/settings/user/friendRequests', 'Friend Requests'],
    ['/settings/user/friendRequests/received', 'Friend Requests'],
    ['/settings/user/friendRequests/sent', 'Friend Requests'],
  ])('highlights only the open section on %s', async (path, section) => {
    const wrapper = await mountSections(path)

    expect(highlighted(wrapper)).toEqual([section])
  })

  it('navigates to a section and moves the highlight when it is clicked', async () => {
    const wrapper = await mountSections('/settings/user/profile')

    await link(wrapper, 'Server Invitations').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('userServerInvitations')
    expect(highlighted(wrapper)).toEqual(['Server Invitations'])
  })
})
