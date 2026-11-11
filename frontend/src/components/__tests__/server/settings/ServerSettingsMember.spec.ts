import { describe, it, expect, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, RouterView, type Router } from 'vue-router'
import ServerSettingsMember from '@/components/server/settings/ServerSettingsMember.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import type { ChatServer } from '@/types'

// ---------- router ----------

// Placeholder for the views rendered inside the router-views
const view = (name: string) =>
  defineComponent({ name, render: () => h('p', { class: 'child-view' }, name) })

// Same structure as the server settings routes of the app, with placeholder views
// instead of the real ones and without guards
function createTestRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/settings/server/:serverId',
        name: 'serverSettings',
        component: defineComponent({ render: () => h(RouterView) }),
        children: [
          { path: 'profile', name: 'serverProfile', component: view('ServerProfile') },
          {
            path: 'members',
            name: 'serverMembers',
            component: ServerSettingsMember,
            children: [
              { path: 'active', name: 'serverActiveMembers', component: view('ActiveMembers') },
              {
                path: 'invitations',
                name: 'serverInvitations',
                component: view('ServerInvitations'),
              },
            ],
          },
        ],
      },
    ],
  })
}

// ---------- helpers ----------

const server = { id: 's1', name: 'My Server' } as ChatServer
const MEMBERS = '/settings/server/s1/members'

let pinia: Pinia
let router: Router

async function mountAt(path: string) {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(defineComponent({ render: () => h(RouterView) }), {
    global: { plugins: [router, pinia] },
  })
  await flushPromises()
  return wrapper
}

async function navigate(path: string) {
  await router.push(path)
  await flushPromises()
}

const sideBar = (wrapper: VueWrapper) => wrapper.find('.members-side-bar')
const membersView = (wrapper: VueWrapper) => wrapper.find('.members-view')
const link = (wrapper: VueWrapper, text: string) =>
  sideBar(wrapper)
    .findAll('a')
    .find((a) => a.text().includes(text))!

// ---------- tests ----------

describe('ServerSettingsMember.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    serverInfoStore().setCurrentServerSettings(server)
    router = createTestRouter()
  })

  describe('side bar', () => {
    it('links back to the server settings', async () => {
      const wrapper = await mountAt(MEMBERS)

      const back = link(wrapper, 'Server Members:')
      expect(back.attributes('href')).toBe('/settings/server/s1')
    })

    it('shows the name of the current server and the title', async () => {
      const wrapper = await mountAt(MEMBERS)

      const titles = sideBar(wrapper)
        .findAll('h2')
        .map((h2) => h2.text())
      expect(titles).toEqual(['Server: My Server', 'Server Members:'])
    })

    it('links to the active members and the invitations of the same server', async () => {
      const wrapper = await mountAt(MEMBERS)

      expect(link(wrapper, 'Active Members').attributes('href')).toBe(`${MEMBERS}/active`)
      expect(link(wrapper, 'Server Invitations').attributes('href')).toBe(`${MEMBERS}/invitations`)
    })

    it('does not highlight any link on the members page', async () => {
      const wrapper = await mountAt(MEMBERS)

      expect(link(wrapper, 'Active Members').classes()).not.toContain('background-light')
      expect(link(wrapper, 'Server Invitations').classes()).not.toContain('background-light')
    })

    it.each([
      ['active', 'Active Members', 'Server Invitations'],
      ['invitations', 'Server Invitations', 'Active Members'],
    ])('highlights only the link of the current page (%s)', async (path, current, other) => {
      const wrapper = await mountAt(`${MEMBERS}/${path}`)

      expect(link(wrapper, current).classes()).toContain('background-light')
      expect(link(wrapper, other).classes()).not.toContain('background-light')
    })

    it('moves the highlight when navigating to the other page', async () => {
      const wrapper = await mountAt(`${MEMBERS}/active`)

      await navigate(`${MEMBERS}/invitations`)

      expect(link(wrapper, 'Server Invitations').classes()).toContain('background-light')
      expect(link(wrapper, 'Active Members').classes()).not.toContain('background-light')
    })

    it('navigates to the page of a link when it is clicked', async () => {
      const wrapper = await mountAt(MEMBERS)

      await link(wrapper, 'Server Invitations').trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('serverInvitations')
      expect(membersView(wrapper).text()).toBe('ServerInvitations')
    })
  })

  describe('members view', () => {
    it('is empty on the members page', async () => {
      const wrapper = await mountAt(MEMBERS)

      expect(membersView(wrapper).find('.child-view').exists()).toBe(false)
    })

    it.each([
      ['active', 'ActiveMembers'],
      ['invitations', 'ServerInvitations'],
    ])('shows the view of the child page (%s)', async (path, viewName) => {
      const wrapper = await mountAt(`${MEMBERS}/${path}`)

      expect(membersView(wrapper).text()).toBe(viewName)
    })
  })

  describe('mobile layout', () => {
    it('shows the list on the members page', async () => {
      const wrapper = await mountAt(MEMBERS)

      expect(sideBar(wrapper).classes()).toContain('settings-list')
      expect(membersView(wrapper).classes()).toContain('settings-list')
    })

    it.each(['active', 'invitations'])('shows the view on a child page (%s)', async (path) => {
      const wrapper = await mountAt(`${MEMBERS}/${path}`)

      expect(sideBar(wrapper).classes()).toContain('settings-view')
      expect(membersView(wrapper).classes()).toContain('settings-view')
    })

    it('switches between the list and the view when navigating', async () => {
      const wrapper = await mountAt(MEMBERS)

      await navigate(`${MEMBERS}/active`)
      expect(sideBar(wrapper).classes()).toContain('settings-view')
      expect(sideBar(wrapper).classes()).not.toContain('settings-list')

      await navigate(MEMBERS)
      expect(sideBar(wrapper).classes()).toContain('settings-list')
      expect(sideBar(wrapper).classes()).not.toContain('settings-view')
    })
  })
})
