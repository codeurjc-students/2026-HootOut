import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ChatMessages from '@/components/chat/chatConversation/ChatMessages.vue'
import { conversationStore } from '@/stores/conversation'
import { userInfoStore } from '@/stores/userInfo'
import { dateTimeMapper } from '@/services/common/utils'
import type { ChatMessage, MessageAttachment, PageResult } from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  messageService: { getMessagesByConversationId: vi.fn() },
  ws: { subscribe: vi.fn(), unsubscribe: vi.fn(), addPendingSubscription: vi.fn() },
}))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: mocks.messageService }))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))

const { messageService } = mocks

// ---------- stubs ----------

const FilePreviewListStub = defineComponent({
  name: 'FilePreviewList',
  props: { files: Array, canDownload: Boolean, canRemove: Boolean },
  setup: () => () => h('div', { class: 'file-preview-list-stub' }),
})

// ---------- helpers ----------

function message(id: string, author = 'alice', overrides: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id,
    author: { id: `${author}-id`, username: author },
    content: `content ${id}`,
    chatConversationId: 'conv-1',
    attachments: [],
    createdAt: '2026-11-21T15:00:00',
    modifiedAt: null,
    ...overrides,
  }
}

function page(items: ChatMessage[], next: string | null = null): PageResult<ChatMessage> {
  return { items, next, total: items.length, pageSize: 20 }
}

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

let pinia: Pinia

// Opens the conversation "conv-1" with its first page of messages, as the app does
async function openConversation(messages: ChatMessage[], next: string | null = null) {
  messageService.getMessagesByConversationId.mockResolvedValueOnce(page(messages, next))
  const store = conversationStore()
  await store.selectConversation('conv-1')
  vi.clearAllMocks()
  return store
}

async function mountMessages() {
  const wrapper = mount(ChatMessages, {
    global: { plugins: [pinia], stubs: { FilePreviewList: FilePreviewListStub } },
  })
  await flushPromises()
  return wrapper
}

const messages = (wrapper: VueWrapper) => wrapper.findAll('.chat-message')
const pending = (wrapper: VueWrapper) => wrapper.findAll('.pending-message')
const olderButton = (wrapper: VueWrapper) => wrapper.find('button.get-more-messages-btn')

// ---------- tests ----------

describe('ChatMessages.vue', () => {
  let warn: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    vi.resetAllMocks()
    userInfoStore().setUser({ id: 'me-id', username: 'me' })
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
    warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.unstubAllEnvs()
    // No Vue warnings (e.g. duplicated keys)
    expect(warn).not.toHaveBeenCalled()
    warn.mockRestore()
  })

  describe('messages', () => {
    it('shows nothing when no conversation is open', async () => {
      const wrapper = await mountMessages()

      expect(messages(wrapper)).toHaveLength(0)
      expect(pending(wrapper)).toHaveLength(0)
      expect(olderButton(wrapper).exists()).toBe(false)
    })

    it('shows the messages, newest first in the DOM (the list is reversed by CSS)', async () => {
      await openConversation([message('m2'), message('m1')])

      const wrapper = await mountMessages()

      expect(messages(wrapper).map((m) => m.text())).toEqual([
        expect.stringContaining('content m2'),
        expect.stringContaining('content m1'),
      ])
      expect(wrapper.find('#messages-list').classes()).toContain('flex-col-reverse')
    })

    it('shows the author and the local date of each message', async () => {
      await openConversation([message('m1', 'alice', { createdAt: '2026-11-21T15:00:00' })])

      const wrapper = await mountMessages()

      const text = messages(wrapper)[0]!.text()
      expect(text).toContain('alice')
      expect(text).toContain(dateTimeMapper('2026-11-21T15:00:00'))
    })

    it('shows no date when the message has none', async () => {
      await openConversation([message('m1', 'alice', { createdAt: null })])

      const wrapper = await mountMessages()

      expect(messages(wrapper)[0]!.find('.text-sm').text()).toBe('')
    })

    it('highlights only the messages of the current user', async () => {
      await openConversation([message('m2', 'me'), message('m1', 'alice')])

      const wrapper = await mountMessages()

      expect(messages(wrapper).map((m) => m.classes().includes('background-soft'))).toEqual([
        true,
        false,
      ])
    })

    it('shows the new messages that arrive', async () => {
      const store = await openConversation([message('m1')])
      const wrapper = await mountMessages()

      store.currentMessages.unshift(message('m2'))
      await flushPromises()

      expect(messages(wrapper)).toHaveLength(2)
    })
  })

  describe('attachments', () => {
    const attachment: MessageAttachment = { id: 'a1', mediaFileId: 'f1', fileName: 'photo.png' }

    it('shows the attachments of a message, ready to download', async () => {
      await openConversation([message('m1', 'alice', { attachments: [attachment] })])

      const wrapper = await mountMessages()

      const preview = messages(wrapper)[0]!.findComponent(FilePreviewListStub)
      expect(preview.props('files')).toEqual([attachment])
      expect(preview.props('canDownload')).toBe(true)
    })

    it('shows no preview for a message without attachments', async () => {
      await openConversation([message('m1')])

      const wrapper = await mountMessages()

      expect(wrapper.findComponent(FilePreviewListStub).exists()).toBe(false)
    })

    it('does not fail when the attachments are null', async () => {
      await openConversation([
        message('m1', 'alice', { attachments: null as unknown as MessageAttachment[] }),
      ])

      const wrapper = await mountMessages()

      expect(messages(wrapper)).toHaveLength(1)
      expect(wrapper.findComponent(FilePreviewListStub).exists()).toBe(false)
    })
  })

  describe('pending messages', () => {
    const sending = (content: string) =>
      ({ content, attachments: [], createdAt: '2026-11-21T16:00:00Z' }) as unknown as ChatMessage

    it('shows the pending messages of the open conversation with the current user and the date', async () => {
      const store = await openConversation([message('m1')])
      store.addPendingMessage('ack-1', sending('hello'))

      const wrapper = await mountMessages()

      expect(pending(wrapper)).toHaveLength(1)
      expect(pending(wrapper)[0]!.text()).toContain('me')
      expect(pending(wrapper)[0]!.text()).toContain(dateTimeMapper('2026-11-21T16:00:00Z'))
      expect(pending(wrapper)[0]!.text()).toContain('hello')
      expect(pending(wrapper)[0]!.classes()).toContain('opacity-25')
    })

    it('shows several pending messages without id (keyed by the id of what was sent)', async () => {
      const store = await openConversation([])
      store.addPendingMessage('ack-1', sending('one'))
      store.addPendingMessage('ack-2', sending('two'))

      const wrapper = await mountMessages()

      expect(pending(wrapper).map((p) => p.text())).toEqual([
        expect.stringContaining('one'),
        expect.stringContaining('two'),
      ])
    })

    it('does not show the pending messages of another conversation', async () => {
      const store = await openConversation([])
      store.pendingMessages['conv-2'] = { 'ack-1': sending('elsewhere') }

      const wrapper = await mountMessages()

      expect(pending(wrapper)).toHaveLength(0)
    })

    it('removes a pending message when it is confirmed', async () => {
      const store = await openConversation([])
      store.addPendingMessage('ack-1', sending('hello'))
      const wrapper = await mountMessages()

      store.removePendingMessage('ack-1')
      await flushPromises()

      expect(pending(wrapper)).toHaveLength(0)
    })
  })

  describe('older messages', () => {
    it('shows the button only when there are older messages', async () => {
      await openConversation([message('m1')], 'cursor-2')

      const wrapper = await mountMessages()

      expect(olderButton(wrapper).text()).toBe('Get Older Messages')
      expect(olderButton(wrapper).attributes('type')).toBe('button')
    })

    it('hides the button on the last page', async () => {
      await openConversation([message('m1')], null)

      const wrapper = await mountMessages()

      expect(olderButton(wrapper).exists()).toBe(false)
    })

    it('loads the next page and adds it after the current messages', async () => {
      await openConversation([message('m2')], 'cursor-2')
      messageService.getMessagesByConversationId.mockResolvedValueOnce(page([message('m1')]))
      const wrapper = await mountMessages()

      await olderButton(wrapper).trigger('click')
      await flushPromises()

      expect(messageService.getMessagesByConversationId).toHaveBeenCalledExactlyOnceWith(
        'conv-1',
        'cursor-2',
        20
      )
      expect(messages(wrapper).map((m) => m.text())).toEqual([
        expect.stringContaining('content m2'),
        expect.stringContaining('content m1'),
      ])
      expect(olderButton(wrapper).exists()).toBe(false)
    })

    it('disables the button while loading and ignores a second click', async () => {
      await openConversation([message('m2')], 'cursor-2')
      const request = deferred<PageResult<ChatMessage>>()
      messageService.getMessagesByConversationId.mockReturnValueOnce(request.promise)
      const wrapper = await mountMessages()

      await olderButton(wrapper).trigger('click')
      expect(olderButton(wrapper).attributes('disabled')).toBeDefined()
      olderButton(wrapper).element.removeAttribute('disabled')
      await olderButton(wrapper).trigger('click')

      request.resolve(page([message('m1')], 'cursor-3'))
      await flushPromises()
      expect(messageService.getMessagesByConversationId).toHaveBeenCalledTimes(1)
      expect(olderButton(wrapper).attributes('disabled')).toBeUndefined()
    })

    it('keeps the button enabled to retry when loading fails', async () => {
      await openConversation([message('m2')], 'cursor-2')
      messageService.getMessagesByConversationId.mockRejectedValueOnce(new Error('Network down'))
      const wrapper = await mountMessages()

      await olderButton(wrapper).trigger('click')
      await flushPromises()

      expect(messages(wrapper)).toHaveLength(1)
      expect(olderButton(wrapper).exists()).toBe(true)
      expect(olderButton(wrapper).attributes('disabled')).toBeUndefined()
    })
  })
})
