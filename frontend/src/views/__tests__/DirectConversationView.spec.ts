import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import DirectConversationView from '@/views/DirectConversationView.vue'
import { directConversationStore } from '@/stores/directConversation'
import { conversationStore } from '@/stores/conversation'
import { userInfoStore } from '@/stores/userInfo'
import type { ChatMessage, DirectConversation, PageResult } from '@/types'

// ---------- mocks ----------

const conversationService = vi.hoisted(() => ({ getDirectConversationById: vi.fn() }))
vi.mock('@/services/conversationService/ConversationService', () => ({
  default: conversationService,
}))

const chatMessageService = vi.hoisted(() => ({ getMessagesByConversationId: vi.fn() }))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: chatMessageService }))

const wsMessageService = vi.hoisted(() => ({
  tryReconnect: vi.fn(),
  subscribe: vi.fn(),
  unsubscribe: vi.fn(),
  addPendingSubscription: vi.fn(),
}))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({
  default: wsMessageService,
}))

// Children replaced by stubs: only the props the view passes matter here
const ChatConversationViewStub = defineComponent({
  name: 'ChatConversationView',
  props: { chatName: String, backLink: Object },
  setup: (props) => () => h('div', { class: 'chat-conversation' }, props.chatName ?? 'Start a new chat'),
})
const stubs = {
  ChatSideBar: defineComponent({ render: () => h('aside') }),
  FriendsBar: defineComponent({ render: () => h('div') }),
  ChatConversationView: ChatConversationViewStub,
}

// ---------- helpers ----------

const me = { id: 'me', username: 'testuser' }
const friend = { id: 'u1', username: 'user1' }

function makeDirectConversation(id: string, user = friend): DirectConversation {
  return { id, chatConversationId: `chat-${id}`, user1: me, user2: user }
}

const emptyPage: PageResult<ChatMessage> = { items: [], next: null, pageSize: 20, total: 0 }

// A promise the test resolves or rejects when it wants
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

let router: Router
let pinia: Pinia

function createTestRouter() {
  const placeholder = defineComponent({ render: () => h('p') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/chat', name: 'chat', component: placeholder },
      {
        path: '/chat/direct/:directConversationId?',
        name: 'directConversation',
        component: placeholder,
      },
    ],
  })
}

async function mountView(path: string) {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(DirectConversationView, { global: { plugins: [router, pinia], stubs } })
  await flushPromises()
  return wrapper
}

const chatName = (wrapper: VueWrapper) =>
  wrapper.findComponent(ChatConversationViewStub).props('chatName')

// ---------- tests ----------

describe('DirectConversationView.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    router = createTestRouter()
    pinia = createPinia()
    setActivePinia(pinia)
    userInfoStore().user = me
    chatMessageService.getMessagesByConversationId.mockResolvedValue(emptyPage)
  })

  it('connects the websocket when mounted', async () => {
    await mountView('/chat/direct')

    expect(wsMessageService.tryReconnect).toHaveBeenCalledOnce()
  })

  describe('without a conversation in the url', () => {
    it('shows no chat and requests nothing', async () => {
      const wrapper = await mountView('/chat/direct')

      expect(chatName(wrapper)).toBeUndefined()
      expect(conversationService.getDirectConversationById).not.toHaveBeenCalled()
      expect(chatMessageService.getMessagesByConversationId).not.toHaveBeenCalled()
      expect(wrapper.find('section').classes()).toContain('view-channels')
    })

    it('closes the conversation that was open', async () => {
      directConversationStore().selectFriendConversation({
        directConversationId: 'dc1',
        chatConversationId: 'chat-dc1',
        friend,
      })
      const conversation = conversationStore()
      conversation.currentConversationId = 'chat-dc1'

      const wrapper = await mountView('/chat/direct')

      expect(directConversationStore().currentConversation).toBeUndefined()
      expect(conversation.currentConversationId).toBe('')
      expect(wsMessageService.unsubscribe).toHaveBeenCalledWith('chat-dc1')
      expect(chatName(wrapper)).toBeUndefined()
    })
  })

  describe('with a conversation in the url', () => {
    it('requests the conversation after a refresh and opens its chat conversation', async () => {
      conversationService.getDirectConversationById.mockResolvedValue(makeDirectConversation('dc1'))

      const wrapper = await mountView('/chat/direct/dc1')

      expect(conversationService.getDirectConversationById).toHaveBeenCalledExactlyOnceWith('dc1')
      expect(wsMessageService.subscribe).toHaveBeenCalledWith('chat-dc1', expect.any(Function))
      expect(chatMessageService.getMessagesByConversationId).toHaveBeenCalledWith('chat-dc1')
      expect(conversationStore().currentConversationId).toBe('chat-dc1')
      expect(chatName(wrapper)).toBe('user1')
      expect(wrapper.find('section').classes()).toContain('view-chat')
    })

    it('does not request a conversation selected from the friends list', async () => {
      directConversationStore().selectFriendConversation({
        directConversationId: 'dc1',
        chatConversationId: 'chat-dc1',
        friend,
      })

      const wrapper = await mountView('/chat/direct/dc1')

      expect(conversationService.getDirectConversationById).not.toHaveBeenCalled()
      expect(chatMessageService.getMessagesByConversationId).toHaveBeenCalledWith('chat-dc1')
      expect(chatName(wrapper)).toBe('user1')
    })

    it('goes back to the friends list when the API returns no content', async () => {
      conversationService.getDirectConversationById.mockResolvedValue(undefined)

      const wrapper = await mountView('/chat/direct/dc1')

      expect(router.currentRoute.value.fullPath).toBe('/chat/direct')
      expect(chatMessageService.getMessagesByConversationId).not.toHaveBeenCalled()
      expect(wsMessageService.subscribe).not.toHaveBeenCalled()
      expect(chatName(wrapper)).toBeUndefined()
    })

    it('goes back to the friends list when the conversation cannot be loaded', async () => {
      conversationService.getDirectConversationById.mockRejectedValue(new Error('Network down'))

      await mountView('/chat/direct/dc1')

      expect(router.currentRoute.value.fullPath).toBe('/chat/direct')
      expect(chatMessageService.getMessagesByConversationId).not.toHaveBeenCalled()
    })

    it('ignores a conversation that finishes loading after the route changed', async () => {
      const first = deferred<DirectConversation>()
      conversationService.getDirectConversationById
        .mockReturnValueOnce(first.promise)
        .mockResolvedValueOnce(makeDirectConversation('dc2', { id: 'u2', username: 'user2' }))
      const wrapper = await mountView('/chat/direct/dc1')

      await router.push('/chat/direct/dc2')
      await flushPromises()
      first.resolve(makeDirectConversation('dc1'))
      await flushPromises()

      expect(router.currentRoute.value.fullPath).toBe('/chat/direct/dc2')
      expect(chatMessageService.getMessagesByConversationId).toHaveBeenCalledExactlyOnceWith(
        'chat-dc2'
      )
      expect(conversationStore().currentConversationId).toBe('chat-dc2')
      expect(chatName(wrapper)).toBe('user2')
    })
  })
})
