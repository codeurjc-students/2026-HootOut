import { describe, it, expect, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter, RouterView, type Router } from 'vue-router'
import FriendRequestsSettings from '@/components/user/settings/FriendRequestsSettings.vue'

// ---------- helpers ----------

let router: Router

// Same structure as the user settings routes of the app, with test components
function createTestRouter() {
  const page = (text: string) =>
    defineComponent({ render: () => h('p', { class: 'child-page' }, text) })
  const nested = defineComponent({ render: () => h(RouterView) })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/settings/user',
        name: 'userSettings',
        component: nested,
        children: [
          { path: 'profile', name: 'userSettingsProfile', component: page('Profile page') },
          {
            path: 'friendRequests',
            name: 'friendRequests',
            component: FriendRequestsSettings,
            children: [
              {
                path: 'received',
                name: 'receivedFriendRequests',
                component: page('Received page'),
              },
              { path: 'sent', name: 'sentFriendRequests', component: page('Sent page') },
            ],
          },
        ],
      },
    ],
  })
}

// Mounts the app's router-view so the nested routes render as in the app
async function mountAt(path: string) {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(RouterView, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

const settings = (wrapper: VueWrapper) => wrapper.findComponent(FriendRequestsSettings)
// The two sides are found by position, so the tests do not depend on their CSS class names
const sideBar = (wrapper: VueWrapper) => settings(wrapper).findAll(':scope > div')[0]!
const view = (wrapper: VueWrapper) => settings(wrapper).findAll(':scope > div')[1]!
const link = (wrapper: VueWrapper, text: string) =>
  settings(wrapper)
    .findAll('a')
    .find((a) => a.text() === text)!
const highlighted = (wrapper: VueWrapper) =>
  settings(wrapper)
    .findAll('a')
    .filter((a) => a.classes().includes('background-light'))
    .map((a) => a.text())

// ---------- tests ----------

describe('FriendRequestsSettings.vue', () => {
  beforeEach(() => {
    router = createTestRouter()
  })

  describe('header', () => {
    it('links back to the user settings with the page title', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      const back = settings(wrapper)
        .findAll('a')
        .find((a) => a.text().includes('Friend Requests:'))!
      expect(back.attributes('href')).toBe('/settings/user')
    })

    it('shows the title of the side bar', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      expect(settings(wrapper).find('h2').text()).toBe('Friend Requests:')
    })
  })

  describe('side bar links', () => {
    it('link to the received and sent friend requests', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      expect(link(wrapper, 'Received Friend Requests').attributes('href')).toBe(
        '/settings/user/friendRequests/received'
      )
      expect(link(wrapper, 'Sent Friend Requests').attributes('href')).toBe(
        '/settings/user/friendRequests/sent'
      )
    })

    it('highlight nothing on the friend requests page itself', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      expect(highlighted(wrapper)).toEqual([])
    })

    it.each([
      ['/settings/user/friendRequests/received', 'Received Friend Requests'],
      ['/settings/user/friendRequests/sent', 'Sent Friend Requests'],
    ])('highlight only the open list on %s', async (path, text) => {
      const wrapper = await mountAt(path)

      expect(highlighted(wrapper)).toEqual([text])
    })

    it('navigate to the list and show it when clicked', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests/received')

      await link(wrapper, 'Sent Friend Requests').trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('sentFriendRequests')
      expect(view(wrapper).text()).toBe('Sent page')
      expect(highlighted(wrapper)).toEqual(['Sent Friend Requests'])
    })
  })

  describe('child view', () => {
    it('is empty on the friend requests page itself', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      expect(view(wrapper).find('.child-page').exists()).toBe(false)
    })

    it.each([
      ['/settings/user/friendRequests/received', 'Received page'],
      ['/settings/user/friendRequests/sent', 'Sent page'],
    ])('shows the page of the child route on %s', async (path, text) => {
      const wrapper = await mountAt(path)

      expect(view(wrapper).text()).toBe(text)
    })
  })

  // On mobile only one side is shown (CSS); these classes decide which one
  describe('mobile classes', () => {
    it('marks both sides as "settings-list" on the friend requests page itself', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      expect(sideBar(wrapper).classes()).toContain('settings-list')
      expect(view(wrapper).classes()).toContain('settings-list')
    })

    it.each(['/settings/user/friendRequests/received', '/settings/user/friendRequests/sent'])(
      'marks both sides as "settings-view" on %s',
      async (path) => {
        const wrapper = await mountAt(path)

        expect(sideBar(wrapper).classes()).toContain('settings-view')
        expect(view(wrapper).classes()).toContain('settings-view')
      }
    )

    it('changes the classes when it navigates to a child route', async () => {
      const wrapper = await mountAt('/settings/user/friendRequests')

      await router.push('/settings/user/friendRequests/sent')
      await flushPromises()

      expect(sideBar(wrapper).classes()).toContain('settings-view')
      expect(sideBar(wrapper).classes()).not.toContain('settings-list')
    })
  })
})
