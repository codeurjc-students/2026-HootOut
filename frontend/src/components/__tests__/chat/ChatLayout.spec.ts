import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import ChatLayout from '@/components/chat/ChatLayout.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import {
  ApiError,
  type ChatChannel,
  type ChatMessage,
  type ChatServer,
  type PageResult,
} from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  serverService: { getServerById: vi.fn() },
  messageService: { getMessagesByConversationId: vi.fn() },
  ws: {
    tryReconnect: vi.fn(),
    subscribe: vi.fn(),
    unsubscribe: vi.fn(),
    addPendingSubscription: vi.fn(),
  },
}))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: mocks.serverService }))
vi.mock('@/services/chatChannel/ChatChannelService', () => ({ default: {} }))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: mocks.messageService }))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))

const { serverService, messageService, ws } = mocks

// ---------- stubs ----------

// The three columns have their own tests
const ChatSideBarStub = defineComponent({
  name: 'ChatSideBar',
  setup: () => () => h('aside', { class: 'chat-side-bar-stub' }),
})
const ChatLeftBarStub = defineComponent({
  name: 'ChatLeftBar',
  setup: () => () => h('div', { class: 'chat-left-bar-stub' }),
})
const ChatConversationViewStub = defineComponent({
  name: 'ChatConversationView',
  props: { chatName: String, backLink: Object },
  setup: () => () => h('div', { class: 'chat-conversation-view-stub' }),
})

// ---------- helpers ----------

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

function makeServer(
  id = 's1',
  channels = [makeChannel('c1', 'general'), makeChannel('c2', 'random')]
): ChatServer {
  return {
    id,
    name: `Server ${id}`,
    description: null,
    channels,
    createdAt: null,
    modifiedAt: null,
  }
}

function emptyPage(): PageResult<ChatMessage> {
  return { items: [], next: null, total: 0, pageSize: 20 }
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

// The app remounts ChatLayout on every route change, so each test mounts it on a route
async function mountAt(path: string) {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(ChatLayout, {
    global: {
      plugins: [pinia, router],
      stubs: {
        ChatSideBar: ChatSideBarStub,
        ChatLeftBar: ChatLeftBarStub,
        ChatConversationView: ChatConversationViewStub,
      },
    },
  })
  await flushPromises()
  return wrapper
}

const conversationView = (wrapper: VueWrapper) => wrapper.findComponent(ChatConversationViewStub)

// Classes of the layout and of its three columns
function mobileClasses(wrapper: VueWrapper) {
  return [
    wrapper.find('section'),
    wrapper.findComponent(ChatSideBarStub),
    wrapper.findComponent(ChatLeftBarStub),
    conversationView(wrapper),
  ].map((element) => element.classes().find((c) => c.startsWith('view-')))
}

// ---------- tests ----------

describe('ChatLayout.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    router = createTestRouter()
    vi.resetAllMocks()
    ws.tryReconnect.mockResolvedValue(undefined)
    serverService.getServerById.mockImplementation(async (id: string) => makeServer(id))
    messageService.getMessagesByConversationId.mockResolvedValue(emptyPage())
  })

  it('connects the websocket when it is mounted', async () => {
    await mountAt('/chat')

    expect(ws.tryReconnect).toHaveBeenCalledTimes(1)
  })

  describe('without a server', () => {
    it('does not load any server', async () => {
      await mountAt('/chat')

      expect(serverService.getServerById).not.toHaveBeenCalled()
    })

    it('shows the channels column on mobile and no chat', async () => {
      const wrapper = await mountAt('/chat')

      expect(mobileClasses(wrapper)).toEqual([
        'view-channels',
        'view-channels',
        'view-channels',
        'view-channels',
      ])
      expect(conversationView(wrapper).props('chatName')).toBeUndefined()
    })
  })

  describe('with a server and no channel', () => {
    it('loads the server of the route and its channels', async () => {
      await mountAt('/chat/server/s1')

      const store = serverInfoStore()
      expect(serverService.getServerById).toHaveBeenCalledExactlyOnceWith('s1')
      expect(store.currentServer?.id).toBe('s1')
      expect(store.channels.map((c) => c.id)).toEqual(['c1', 'c2'])
    })

    it('does not open any channel', async () => {
      const wrapper = await mountAt('/chat/server/s1')

      expect(messageService.getMessagesByConversationId).not.toHaveBeenCalled()
      expect(conversationView(wrapper).props('chatName')).toBeUndefined()
      expect(mobileClasses(wrapper)).toEqual([
        'view-channels',
        'view-channels',
        'view-channels',
        'view-channels',
      ])
    })

    it('gives the chat a back link to the server, without channel', async () => {
      const wrapper = await mountAt('/chat/server/s1')

      expect(conversationView(wrapper).props('backLink')).toEqual({
        name: 'server',
        params: { serverId: 's1' },
      })
    })
  })

  describe('with a server and a channel', () => {
    it('opens the channel of the route after loading the server', async () => {
      const wrapper = await mountAt('/chat/server/s1/channel/c2')

      expect(serverInfoStore().currentChatChannel?.id).toBe('c2')
      expect(messageService.getMessagesByConversationId).toHaveBeenCalledExactlyOnceWith(
        'conversation-c2'
      )
      expect(ws.subscribe).toHaveBeenCalledWith('conversation-c2', expect.any(Function))
      expect(conversationView(wrapper).props('chatName')).toBe('random')
    })

    it('shows only the chat on mobile', async () => {
      const wrapper = await mountAt('/chat/server/s1/channel/c2')

      expect(mobileClasses(wrapper)).toEqual(['view-chat', 'view-chat', 'view-chat', 'view-chat'])
    })

    it('opens nothing when the channel is not in the server', async () => {
      const wrapper = await mountAt('/chat/server/s1/channel/unknown')

      expect(messageService.getMessagesByConversationId).not.toHaveBeenCalled()
      expect(conversationView(wrapper).props('chatName')).toBeUndefined()
    })

    it('stays in the channel when its messages cannot be loaded', async () => {
      messageService.getMessagesByConversationId.mockRejectedValue(new Error('Network down'))

      const wrapper = await mountAt('/chat/server/s1/channel/c2')

      expect(router.currentRoute.value.name).toBe('channel')
      expect(conversationView(wrapper).props('chatName')).toBe('random')
    })
  })

  describe('when the server cannot be loaded', () => {
    it.each([
      ['it does not exist', new ApiError(404, 'Not found')],
      ['the user cannot open it', new ApiError(403, 'Forbidden')],
      ['the request fails', new Error('Network down')],
    ])('goes back to the chat page when %s', async (_, error) => {
      serverService.getServerById.mockRejectedValue(error)

      await mountAt('/chat/server/s1/channel/c2')

      expect(router.currentRoute.value.name).toBe('chat')
      expect(messageService.getMessagesByConversationId).not.toHaveBeenCalled()
    })
  })

  it('loads the new server when only the server of the route changes', async () => {
    await mountAt('/chat/server/s1')

    await router.push('/chat/server/s2')
    await flushPromises()

    expect(serverService.getServerById).toHaveBeenLastCalledWith('s2')
    expect(serverInfoStore().currentServer?.id).toBe('s2')
  })
})
