import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, type Directive } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import ChatSideBar from '@/components/chat/ChatSideBar.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import type { ChatServer } from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({ serverService: { getServerById: vi.fn() } }))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: mocks.serverService }))
vi.mock('@/services/chatChannel/ChatChannelService', () => ({ default: {} }))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: {} }))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({ default: {} }))

// ---------- stubs ----------

// Modal replaced by a stub that exposes open() and close() spies and always renders its body
const modal = { open: vi.fn(), close: vi.fn() }
const ModalStub = defineComponent({
  name: 'Modal',
  props: { title: { type: String, required: true } },
  setup(props, { expose, slots }) {
    expose(modal)
    return () => h('div', { class: 'modal-stub' }, [h('h3', props.title), slots.body?.()])
  },
})

const CreateServerFormStub = defineComponent({
  name: 'CreateServerForm',
  emits: ['success'],
  setup: () => () => h('form', { class: 'create-server-form-stub' }),
})

const ServerListStub = defineComponent({
  name: 'ServerList',
  setup: () => () => h('div', { class: 'server-list-stub' }),
})

// The tooltip directive (registered globally in the app) only records how it is used
const tooltipBindings: { value: unknown; arg?: string }[] = []
const vTooltipStub: Directive = {
  mounted: (_el, binding) => {
    tooltipBindings.push({ value: binding.value, arg: binding.arg })
  },
}

// ---------- helpers ----------

function makeServer(id = 's9'): ChatServer {
  return {
    id,
    name: 'New Server',
    description: null,
    channels: [],
    createdAt: null,
    modifiedAt: null,
  }
}

let pinia: Pinia
let router: Router

function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/chat', name: 'chat', component: placeholder },
      {
        path: '/chat/direct/:chatConversationId?',
        name: 'directConversation',
        component: placeholder,
      },
      { path: '/chat/server/:serverId?', name: 'server', component: placeholder },
    ],
  })
}

async function mountSideBar() {
  await router.push('/chat')
  await router.isReady()
  const wrapper = mount(ChatSideBar, {
    global: {
      plugins: [pinia, router],
      stubs: {
        Modal: ModalStub,
        CreateServerForm: CreateServerFormStub,
        ServerList: ServerListStub,
      },
      directives: { tooltip: vTooltipStub },
    },
  })
  await flushPromises()
  return wrapper
}

const directMessagesLink = (wrapper: VueWrapper) => wrapper.find('a#directConversationLink')
const createServerButton = (wrapper: VueWrapper) =>
  wrapper.find('button[aria-label="Create server"]')

// ---------- tests ----------

describe('ChatSideBar.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    router = createTestRouter()
    vi.resetAllMocks()
    tooltipBindings.length = 0
  })

  describe('direct messages link', () => {
    it('links to the direct messages', async () => {
      const wrapper = await mountSideBar()

      expect(directMessagesLink(wrapper).attributes('href')).toBe('/chat/direct')
      expect(directMessagesLink(wrapper).attributes('aria-label')).toBe('Direct Messages')
    })

    it('has a tooltip on the right', async () => {
      await mountSideBar()

      expect(tooltipBindings).toEqual([{ value: 'Direct Messages', arg: 'right' }])
    })
  })

  it('shows the list of servers', async () => {
    const wrapper = await mountSideBar()

    expect(wrapper.find('#server-list').findComponent(ServerListStub).exists()).toBe(true)
  })

  describe('create server', () => {
    it('has a labelled button', async () => {
      const wrapper = await mountSideBar()

      expect(createServerButton(wrapper).attributes('type')).toBe('button')
      expect(createServerButton(wrapper).attributes('id')).toBe('new-server-btn')
    })

    it('puts the form in a modal titled "Create New Server"', async () => {
      const wrapper = await mountSideBar()

      expect(wrapper.find('.modal-stub h3').text()).toBe('Create New Server')
      expect(wrapper.find('.modal-stub').findComponent(CreateServerFormStub).exists()).toBe(true)
    })

    it('opens the modal when the button is clicked', async () => {
      const wrapper = await mountSideBar()

      await createServerButton(wrapper).trigger('click')

      expect(modal.open).toHaveBeenCalledTimes(1)
    })

    describe('when the server is created', () => {
      async function createServer(wrapper: VueWrapper, server = makeServer()) {
        wrapper.findComponent(CreateServerFormStub).vm.$emit('success', server)
        await flushPromises()
      }

      it('adds it to the list of servers', async () => {
        const wrapper = await mountSideBar()

        await createServer(wrapper)

        expect(serverInfoStore().servers.map((s) => s.id)).toEqual(['s9'])
      })

      it('closes the modal', async () => {
        const wrapper = await mountSideBar()

        await createServer(wrapper)

        expect(modal.close).toHaveBeenCalledTimes(1)
      })

      it('navigates to the new server', async () => {
        const wrapper = await mountSideBar()

        await createServer(wrapper)

        expect(router.currentRoute.value.name).toBe('server')
        expect(router.currentRoute.value.params).toEqual({ serverId: 's9' })
      })

      it('leaves loading the server to the chat view (a single request)', async () => {
        const wrapper = await mountSideBar()

        await createServer(wrapper)

        expect(mocks.serverService.getServerById).not.toHaveBeenCalled()
      })
    })
  })
})
