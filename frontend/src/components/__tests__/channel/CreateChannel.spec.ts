import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import CreateChannel from '@/components/channel/CreateChannel.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import type { ChatChannel, ChatServer } from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  ws: { subscribe: vi.fn(), unsubscribe: vi.fn(), addPendingSubscription: vi.fn() },
}))
vi.mock('@/services/chatChannel/ChatChannelService', () => ({ default: {} }))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: {} }))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: {} }))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))

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

// CreateChannelForm replaced by a stub: the tests emit its success event directly
const CreateChannelFormStub = defineComponent({
  name: 'CreateChannelForm',
  props: { server: Object },
  emits: ['success'],
  setup: () => () => h('form', { class: 'create-channel-form-stub' }),
})

// ---------- helpers ----------

function makeServer(overrides: Partial<ChatServer> = {}): ChatServer {
  return {
    id: 's1',
    name: 'My Server',
    description: null,
    channels: [],
    createdAt: null,
    modifiedAt: null,
    ...overrides,
  }
}

function makeChannel(id: string, name = id): ChatChannel {
  return {
    id,
    serverId: 's1',
    chatConversationId: `conversation-${id}`,
    name,
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
      { path: '/chat/server/:serverId?', name: 'server', component: placeholder },
      {
        path: '/chat/server/:serverId/channel/:channelId',
        name: 'channel',
        component: placeholder,
      },
    ],
  })
}

// Store with the current server and its channels (no server when null)
function setUpStore(server: ChatServer | null, channels: ChatChannel[] = []) {
  const store = serverInfoStore()
  if (server) store.setCurrentServerSettings({ ...server, channels })
  store.setServerChannels(channels)
  return store
}

async function mountCreateChannel(path = '/chat/server/s1') {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(CreateChannel, {
    global: {
      plugins: [pinia, router],
      stubs: { Modal: ModalStub, CreateChannelForm: CreateChannelFormStub },
    },
  })
  await flushPromises()
  return wrapper
}

const addButton = (wrapper: VueWrapper) => wrapper.find('button[aria-label="Create channel"]')
const form = (wrapper: VueWrapper) => wrapper.findComponent(CreateChannelFormStub)

async function emitSuccess(wrapper: VueWrapper, channel: ChatChannel) {
  form(wrapper).vm.$emit('success', channel)
  await flushPromises()
}

// ---------- tests ----------

describe('CreateChannel.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    router = createTestRouter()
    vi.resetAllMocks()
  })

  describe('header', () => {
    it('shows the title and a labelled button to create a channel', async () => {
      setUpStore(makeServer())

      const wrapper = await mountCreateChannel()

      expect(wrapper.find('.add-channel').text()).toContain('Channels')
      expect(addButton(wrapper).exists()).toBe(true)
    })

    it('hides the button when there is no current server', async () => {
      setUpStore(null)

      const wrapper = await mountCreateChannel('/chat')

      expect(wrapper.find('.add-channel').text()).toContain('Channels')
      expect(addButton(wrapper).exists()).toBe(false)
    })

    it('shows the button when the current server is loaded later', async () => {
      const store = setUpStore(null)
      const wrapper = await mountCreateChannel()

      store.setCurrentServerSettings(makeServer())
      await flushPromises()

      expect(addButton(wrapper).exists()).toBe(true)
    })
  })

  describe('modal', () => {
    it('has the title "Create New Channel"', async () => {
      setUpStore(makeServer())

      const wrapper = await mountCreateChannel()

      expect(wrapper.find('.modal-stub h3').text()).toBe('Create New Channel')
    })

    it('opens when the button is clicked', async () => {
      setUpStore(makeServer())
      const wrapper = await mountCreateChannel()

      await addButton(wrapper).trigger('click')

      expect(modal.open).toHaveBeenCalledTimes(1)
    })

    it('passes the current server to the form', async () => {
      const server = makeServer()
      setUpStore(server)

      const wrapper = await mountCreateChannel()

      expect(form(wrapper).props('server')).toMatchObject({ id: 's1', name: 'My Server' })
    })
  })

  describe('when the channel is created', () => {
    it('adds the channel to the list of the server', async () => {
      const store = setUpStore(makeServer(), [makeChannel('c1', 'general')])
      const wrapper = await mountCreateChannel()

      await emitSuccess(wrapper, makeChannel('c2', 'random'))

      expect(store.channels.map((c) => c.id)).toEqual(['c1', 'c2'])
    })

    it('closes the modal', async () => {
      setUpStore(makeServer())
      const wrapper = await mountCreateChannel()

      await emitSuccess(wrapper, makeChannel('c2'))

      expect(modal.close).toHaveBeenCalledTimes(1)
    })

    it('navigates to the new channel', async () => {
      setUpStore(makeServer())
      const wrapper = await mountCreateChannel()

      await emitSuccess(wrapper, makeChannel('c2'))

      expect(router.currentRoute.value.name).toBe('channel')
      expect(router.currentRoute.value.params).toEqual({ serverId: 's1', channelId: 'c2' })
    })

    it('leaves opening the channel to the channel view', async () => {
      const store = setUpStore(makeServer())
      const wrapper = await mountCreateChannel()

      await emitSuccess(wrapper, makeChannel('c2'))

      // No subscription and no current channel here: the route does it
      expect(mocks.ws.subscribe).not.toHaveBeenCalled()
      expect(mocks.ws.addPendingSubscription).not.toHaveBeenCalled()
      expect(store.currentChatChannel).toBeUndefined()
    })

    it('closes the modal and navigates when the channel is already in the list', async () => {
      const existing = makeChannel('c2')
      const store = setUpStore(makeServer(), [existing])
      const wrapper = await mountCreateChannel()

      await emitSuccess(wrapper, existing)

      expect(store.channels).toHaveLength(1)
      expect(modal.close).toHaveBeenCalledTimes(1)
      expect(router.currentRoute.value.params).toEqual({ serverId: 's1', channelId: 'c2' })
    })
  })
})
