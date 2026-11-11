import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, type Directive } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import ServerList from '@/components/server/ServerList.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import type { ChatServer, PageResult } from '@/types'

// ---------- mocks ----------

const serverService = vi.hoisted(() => ({ getServers: vi.fn() }))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: serverService }))

// jsdom does not implement IntersectionObserver: trigger() simulates the loader becoming visible
class FakeIntersectionObserver {
  static instances: FakeIntersectionObserver[] = []
  observe = vi.fn()
  unobserve = vi.fn()
  disconnect = vi.fn()

  constructor(private callback: IntersectionObserverCallback) {
    FakeIntersectionObserver.instances.push(this)
  }

  async trigger() {
    await this.callback(
      [{ intersectionRatio: 1 } as IntersectionObserverEntry],
      this as unknown as IntersectionObserver
    )
    await flushPromises()
  }
}

// Records the value and argument each element gets from v-tooltip
const tooltips = new Map<Element, { value: unknown; arg?: string }>()
const tooltipSpy: Directive = {
  mounted: (el, binding) => tooltips.set(el, { value: binding.value, arg: binding.arg }),
  updated: (el, binding) => tooltips.set(el, { value: binding.value, arg: binding.arg }),
}

// ---------- helpers ----------

function chatServer(id: number, name = `server ${id}`): ChatServer {
  return { id: `s${id}`, name } as ChatServer
}

function page(items: ChatServer[], next: string | null = null): PageResult<ChatServer> {
  return { items, next, total: items.length, pageSize: 20 }
}

let pinia: Pinia
let router: Router

function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/chat', name: 'chat', component: placeholder },
      { path: '/chat/server/:serverId?', name: 'server', component: placeholder },
    ],
  })
}

async function mountServerList() {
  const wrapper = mount(ServerList, {
    global: { plugins: [pinia, router], directives: { tooltip: tooltipSpy } },
  })
  await flushPromises()
  return wrapper
}

const observer = () => FakeIntersectionObserver.instances.at(-1)!
const serverLinks = (wrapper: VueWrapper) => wrapper.findAll('a')
const serverIds = (wrapper: VueWrapper) =>
  serverLinks(wrapper).map((a) => a.attributes('server-id'))

// ---------- tests ----------

describe('ServerList.vue', () => {
  beforeEach(async () => {
    FakeIntersectionObserver.instances = []
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver)
    tooltips.clear()
    serverService.getServers.mockReset()
    pinia = createPinia()
    setActivePinia(pinia)
    router = createTestRouter()
    await router.push('/chat')
    await router.isReady()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  describe('loading servers', () => {
    it('clears the servers loaded before it was mounted', async () => {
      serverInfoStore().addChatServer(chatServer(99, 'stale'))

      const wrapper = await mountServerList()

      expect(serverInfoStore().servers).toEqual([])
      expect(serverLinks(wrapper)).toHaveLength(0)
    })

    it('does not request servers until the end of the list is visible', async () => {
      await mountServerList()

      expect(serverService.getServers).not.toHaveBeenCalled()
    })

    it('requests the first page when the end of the list becomes visible', async () => {
      serverService.getServers.mockResolvedValueOnce(
        page([chatServer(1), chatServer(2)], 'cursor-2')
      )
      const wrapper = await mountServerList()

      await observer().trigger()

      expect(serverService.getServers).toHaveBeenCalledWith(null)
      expect(serverIds(wrapper)).toEqual(['s1', 's2'])
    })

    it('requests the next page with the cursor of the previous one', async () => {
      serverService.getServers
        .mockResolvedValueOnce(page([chatServer(1)], 'cursor-2'))
        .mockResolvedValueOnce(page([chatServer(2)], null))
      const wrapper = await mountServerList()

      await observer().trigger()
      await observer().trigger()

      expect(serverService.getServers).toHaveBeenLastCalledWith('cursor-2')
      expect(serverIds(wrapper)).toEqual(['s1', 's2'])
    })

    it('stops requesting servers after the last page', async () => {
      serverService.getServers.mockResolvedValueOnce(page([chatServer(1)], null))
      await mountServerList()

      await observer().trigger()
      await observer().trigger()

      expect(serverService.getServers).toHaveBeenCalledTimes(1)
    })
  })

  describe('server buttons', () => {
    beforeEach(() => {
      serverService.getServers.mockResolvedValueOnce(
        page([chatServer(1, 'vue fans'), chatServer(2, 'React')])
      )
    })

    it('shows the first letter of each server name in upper case', async () => {
      const wrapper = await mountServerList()
      await observer().trigger()

      expect(serverLinks(wrapper).map((a) => a.text())).toEqual(['V', 'R'])
    })

    it('links each button to its server', async () => {
      const wrapper = await mountServerList()
      await observer().trigger()

      expect(serverLinks(wrapper).map((a) => a.attributes('href'))).toEqual([
        '/chat/server/s1',
        '/chat/server/s2',
      ])
    })

    it('labels each button with the server name for screen readers', async () => {
      const wrapper = await mountServerList()
      await observer().trigger()

      expect(serverLinks(wrapper).map((a) => a.attributes('aria-label'))).toEqual([
        'vue fans',
        'React',
      ])
    })

    it('shows the server name in a tooltip on the right', async () => {
      const wrapper = await mountServerList()
      await observer().trigger()

      expect(serverLinks(wrapper).map((a) => tooltips.get(a.element))).toEqual([
        { value: 'vue fans', arg: 'right' },
        { value: 'React', arg: 'right' },
      ])
    })

    it('navigates to the server when its button is clicked', async () => {
      const wrapper = await mountServerList()
      await observer().trigger()

      await serverLinks(wrapper)[1]!.trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('server')
      expect(router.currentRoute.value.params.serverId).toBe('s2')
    })
  })

  describe('current server', () => {
    it('highlights only the button of the current server', async () => {
      serverService.getServers.mockResolvedValueOnce(page([chatServer(1), chatServer(2)]))
      const wrapper = await mountServerList()
      serverInfoStore().setCurrentServerSettings(chatServer(2))
      await observer().trigger()

      expect(serverLinks(wrapper).map((a) => a.classes().includes('background-light'))).toEqual([
        false,
        true,
      ])
    })

    it('shows the current server first while it has not been loaded yet', async () => {
      serverInfoStore().setCurrentServerSettings(chatServer(30))
      serverService.getServers.mockResolvedValueOnce(
        page([chatServer(1), chatServer(2)], 'cursor-2')
      )
      const wrapper = await mountServerList()

      expect(serverIds(wrapper)).toEqual(['s30'])

      await observer().trigger()
      expect(serverIds(wrapper)).toEqual(['s30', 's1', 's2'])
    })

    it('shows the current server only once after it has been loaded', async () => {
      serverInfoStore().setCurrentServerSettings(chatServer(2))
      serverService.getServers.mockResolvedValueOnce(
        page([chatServer(1), chatServer(2), chatServer(3)])
      )
      const wrapper = await mountServerList()

      await observer().trigger()

      expect(serverIds(wrapper)).toEqual(['s1', 's2', 's3'])
    })

    it('shows only the loaded servers when there is no current server', async () => {
      serverService.getServers.mockResolvedValueOnce(page([chatServer(1)]))
      const wrapper = await mountServerList()

      await observer().trigger()

      expect(serverIds(wrapper)).toEqual(['s1'])
    })
  })
})
