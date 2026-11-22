import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import ChatMessageBar from '@/components/chat/chatConversation/ChatMessageBar.vue'
import { conversationStore } from '@/stores/conversation'
import {
  WSMessageType,
  type ChatMessage,
  type PageResult,
  type UploadFileResponse,
  type WebSocketMessage,
} from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  messageService: { getMessagesByConversationId: vi.fn() },
  mediaFileService: { uploadFile: vi.fn() },
  ws: {
    subscribe: vi.fn(),
    unsubscribe: vi.fn(),
    addPendingSubscription: vi.fn(),
    sendMessage: vi.fn(),
  },
}))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: mocks.messageService }))
vi.mock('@/services/mediaFile/MediaFileService', () => ({ default: mocks.mediaFileService }))
vi.mock('@/services/common/websocketsMessage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))

const { ws, mediaFileService } = mocks

// ---------- stubs ----------

const FilePreviewListStub = defineComponent({
  name: 'FilePreviewList',
  props: { files: Array, canRemove: Boolean },
  emits: ['remove'],
  setup: () => () => h('div', { class: 'file-preview-list-stub' }),
})

// ---------- helpers ----------

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

// wsMessageService.sendMessage returns the id of the sent message and a promise for its ack
function mockSend(id = 'ack-1') {
  const ack = deferred<WebSocketMessage>()
  ws.sendMessage.mockReturnValueOnce({ id, promise: ack.promise })
  return ack
}

// The ack the server sends back: the saved message, replying to the sent one
function ackFor(id: string, saved: Partial<ChatMessage>): WebSocketMessage {
  return {
    replyTo: id,
    channel: 'conv-1',
    type: WSMessageType.ChatMessage,
    payload: JSON.stringify(saved),
  }
}

function uploaded(id: string): UploadFileResponse {
  return {
    id,
    contentType: 'image/png',
    fileName: `${id}.png`,
    bytesSize: '10',
    url: `https://files/${id}`,
  }
}

let pinia: Pinia

async function openConversation(conversationId = 'conv-1') {
  const store = conversationStore()
  mocks.messageService.getMessagesByConversationId.mockResolvedValueOnce({
    items: [],
    next: null,
    total: 0,
    pageSize: 20,
  } satisfies PageResult<ChatMessage>)
  await store.selectConversation(conversationId)
  vi.clearAllMocks()
  return store
}

function mountBar(chatName = 'general') {
  return mount(ChatMessageBar, {
    props: { chatName },
    global: { plugins: [pinia], stubs: { FilePreviewList: FilePreviewListStub } },
  })
}

const input = (wrapper: VueWrapper) => wrapper.find<HTMLInputElement>('#message-input')
const fileInput = (wrapper: VueWrapper) => wrapper.find<HTMLInputElement>('#file-input')
const sendButton = (wrapper: VueWrapper) =>
  wrapper.find<HTMLButtonElement>('button[aria-label="Send message"]')
const attachButton = (wrapper: VueWrapper) =>
  wrapper.find<HTMLButtonElement>('button[aria-label="Attach file"]')
const preview = (wrapper: VueWrapper) => wrapper.findComponent(FilePreviewListStub)

async function write(wrapper: VueWrapper, text: string) {
  await input(wrapper).setValue(text)
}

async function pressEnter(wrapper: VueWrapper) {
  await input(wrapper).trigger('keyup.enter')
  await flushPromises()
}

// jsdom does not let tests pick files: the input gets a FileList-like object
async function chooseFile(wrapper: VueWrapper, file: File | null) {
  const files = file ? { 0: file, length: 1, item: () => file } : { length: 0, item: () => null }
  Object.defineProperty(fileInput(wrapper).element, 'files', { value: files, configurable: true })
  await fileInput(wrapper).trigger('change')
  await flushPromises()
}

const photo = () => new File(['x'], 'photo.png', { type: 'image/png' })

// ---------- tests ----------

describe('ChatMessageBar.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    vi.resetAllMocks()
  })

  describe('input', () => {
    it('invites to write a message to the chat', () => {
      const wrapper = mountBar('general')

      expect(input(wrapper).attributes('placeholder')).toBe('Write a message to general')
      expect(input(wrapper).attributes('aria-label')).toBe('Write a message')
    })

    it('has labelled buttons to attach a file and to send', () => {
      const wrapper = mountBar()

      expect(attachButton(wrapper).attributes('type')).toBe('button')
      expect(sendButton(wrapper).attributes('type')).toBe('button')
      expect(sendButton(wrapper).attributes('id')).toBe('wsmessage-btn')
    })

    it.each([
      ['empty', ''],
      ['only spaces', '   '],
    ])('disables the send button when the message is %s', async (_, text) => {
      const wrapper = mountBar()

      await write(wrapper, text)

      expect(sendButton(wrapper).element.disabled).toBe(true)
    })

    it('enables the send button when there is a message', async () => {
      const wrapper = mountBar()

      await write(wrapper, 'hello')

      expect(sendButton(wrapper).element.disabled).toBe(false)
    })
  })

  describe('send', () => {
    it('sends the trimmed message to the open conversation when Enter is pressed', async () => {
      await openConversation('conv-1')
      mockSend()
      const wrapper = mountBar()

      await write(wrapper, '  hello  ')
      await pressEnter(wrapper)

      expect(ws.sendMessage).toHaveBeenCalledExactlyOnceWith('conv-1', {
        content: 'hello',
        attachments: [],
      })
    })

    it('sends the message when the send button is clicked', async () => {
      await openConversation()
      mockSend()
      const wrapper = mountBar()

      await write(wrapper, 'hello')
      await sendButton(wrapper).trigger('click')
      await flushPromises()

      expect(ws.sendMessage).toHaveBeenCalledTimes(1)
    })

    it('empties the input at once and shows the message as pending until the ack', async () => {
      const store = await openConversation()
      mockSend('ack-1')
      const wrapper = mountBar()

      await write(wrapper, 'hello')
      await pressEnter(wrapper)

      expect(input(wrapper).element.value).toBe('')
      const pendingMessage = store.pendingMessages['conv-1']!['ack-1']!
      expect(pendingMessage.content).toBe('hello')
      expect(new Date(pendingMessage.createdAt!).getTime()).not.toBeNaN()
    })

    it('replaces the pending message with the saved one when the ack arrives', async () => {
      const store = await openConversation()
      const ack = mockSend('ack-1')
      const wrapper = mountBar()
      await write(wrapper, 'hello')
      await pressEnter(wrapper)

      ack.resolve(ackFor('ack-1', { id: 'saved-1', content: 'hello' }))
      await flushPromises()

      expect(store.pendingMessages['conv-1']).toEqual({})
      expect(store.currentMessages).toEqual([{ id: 'saved-1', content: 'hello' }])
    })

    it('does nothing, and keeps the text, when no conversation is open', async () => {
      const wrapper = mountBar()

      await write(wrapper, 'hello')
      await pressEnter(wrapper)

      expect(ws.sendMessage).not.toHaveBeenCalled()
      expect(input(wrapper).element.value).toBe('hello')
    })

    it.each([
      ['empty', ''],
      ['only spaces', '   '],
    ])('does nothing when the message is %s', async (_, text) => {
      await openConversation()
      const wrapper = mountBar()

      await write(wrapper, text)
      await pressEnter(wrapper)

      expect(ws.sendMessage).not.toHaveBeenCalled()
    })

    it('lets the user send another message while the first one waits for its ack', async () => {
      const store = await openConversation()
      mockSend('ack-1')
      mockSend('ack-2')
      const wrapper = mountBar()

      await write(wrapper, 'one')
      await pressEnter(wrapper)
      await write(wrapper, 'two')
      await pressEnter(wrapper)

      expect(Object.keys(store.pendingMessages['conv-1']!)).toEqual(['ack-1', 'ack-2'])
    })
  })

  describe('when sending fails', () => {
    it('removes the pending message and puts the text back when the ack fails', async () => {
      const store = await openConversation()
      const ack = mockSend('ack-1')
      const wrapper = mountBar()
      await write(wrapper, 'hello')
      await pressEnter(wrapper)

      ack.reject(new Error('No ack within 5000ms'))
      await flushPromises()

      expect(store.pendingMessages['conv-1']).toEqual({})
      expect(store.currentMessages).toEqual([])
      expect(input(wrapper).element.value).toBe('hello')
    })

    it('puts the text back when the message cannot be sent at all', async () => {
      const store = await openConversation()
      ws.sendMessage.mockImplementationOnce(() => {
        throw new Error('WebSocket is not connected')
      })
      const wrapper = mountBar()

      await write(wrapper, 'hello')
      await pressEnter(wrapper)

      expect(store.pendingMessages['conv-1']).toEqual({})
      expect(input(wrapper).element.value).toBe('hello')
    })

    it('does not overwrite what the user wrote in the meantime', async () => {
      await openConversation()
      const ack = mockSend('ack-1')
      const wrapper = mountBar()
      await write(wrapper, 'hello')
      await pressEnter(wrapper)
      await write(wrapper, 'new text')

      ack.reject(new Error('Connection closed'))
      await flushPromises()

      expect(input(wrapper).element.value).toBe('new text')
    })
  })

  describe('attachments', () => {
    it('opens the file picker when the attach button is clicked', async () => {
      const wrapper = mountBar()
      const click = vi.spyOn(fileInput(wrapper).element, 'click').mockImplementation(() => {})

      await attachButton(wrapper).trigger('click')

      expect(click).toHaveBeenCalledTimes(1)
    })

    it('uploads the chosen file and shows it in the preview, ready to remove', async () => {
      mediaFileService.uploadFile.mockResolvedValue(uploaded('f1'))
      const wrapper = mountBar()
      const file = photo()

      await chooseFile(wrapper, file)

      expect(mediaFileService.uploadFile).toHaveBeenCalledExactlyOnceWith(file)
      expect(preview(wrapper).props('files')).toEqual([
        {
          mediaFileId: 'f1',
          contentType: 'image/png',
          url: 'https://files/f1',
          fileName: 'f1.png',
        },
      ])
      expect(preview(wrapper).props('canRemove')).toBe(true)
    })

    it('empties the file input so the same file can be chosen again', async () => {
      mediaFileService.uploadFile.mockResolvedValue(uploaded('f1'))
      const wrapper = mountBar()
      const setValue = vi.spyOn(fileInput(wrapper).element, 'value', 'set')

      await chooseFile(wrapper, photo())

      expect(setValue).toHaveBeenLastCalledWith('')
    })

    it('does nothing when no file was chosen', async () => {
      const wrapper = mountBar()

      await chooseFile(wrapper, null)

      expect(mediaFileService.uploadFile).not.toHaveBeenCalled()
      expect(preview(wrapper).exists()).toBe(false)
    })

    it('adds nothing when the upload returns no file', async () => {
      mediaFileService.uploadFile.mockResolvedValue(undefined)
      const wrapper = mountBar()

      await chooseFile(wrapper, photo())

      expect(preview(wrapper).exists()).toBe(false)
    })

    it('adds nothing, and lets the user try again, when the upload fails', async () => {
      mediaFileService.uploadFile.mockRejectedValue(new Error('Too large'))
      const wrapper = mountBar()

      await chooseFile(wrapper, photo())

      expect(preview(wrapper).exists()).toBe(false)
      expect(attachButton(wrapper).element.disabled).toBe(false)
    })

    it('removes a file when the preview asks for it', async () => {
      mediaFileService.uploadFile
        .mockResolvedValueOnce(uploaded('f1'))
        .mockResolvedValueOnce(uploaded('f2'))
      const wrapper = mountBar()
      await chooseFile(wrapper, photo())
      await chooseFile(wrapper, photo())

      preview(wrapper).vm.$emit('remove', { mediaFileId: 'f1' })
      await flushPromises()

      expect(
        (preview(wrapper).props('files') as { mediaFileId: string }[]).map((f) => f.mediaFileId)
      ).toEqual(['f2'])
    })

    it('blocks sending and attaching while a file is uploading', async () => {
      await openConversation()
      const upload = deferred<UploadFileResponse>()
      mediaFileService.uploadFile.mockReturnValue(upload.promise)
      const wrapper = mountBar()
      await write(wrapper, 'look at this')

      await chooseFile(wrapper, photo())
      expect(sendButton(wrapper).element.disabled).toBe(true)
      expect(attachButton(wrapper).element.disabled).toBe(true)
      await pressEnter(wrapper)
      expect(ws.sendMessage).not.toHaveBeenCalled()

      upload.resolve(uploaded('f1'))
      await flushPromises()
      expect(sendButton(wrapper).element.disabled).toBe(false)
    })

    it('can send a message with only attachments', async () => {
      await openConversation()
      mediaFileService.uploadFile.mockResolvedValue(uploaded('f1'))
      mockSend()
      const wrapper = mountBar()
      await chooseFile(wrapper, photo())

      expect(sendButton(wrapper).element.disabled).toBe(false)
      await pressEnter(wrapper)

      expect(ws.sendMessage).toHaveBeenCalledExactlyOnceWith('conv-1', {
        content: '',
        attachments: [{ mediaFileId: 'f1' }],
      })
    })

    it('sends only the id of each attachment and removes them from the preview at once', async () => {
      await openConversation()
      mediaFileService.uploadFile
        .mockResolvedValueOnce(uploaded('f1'))
        .mockResolvedValueOnce(uploaded('f2'))
      mockSend()
      const wrapper = mountBar()
      await chooseFile(wrapper, photo())
      await chooseFile(wrapper, photo())

      await write(wrapper, 'photos')
      await pressEnter(wrapper)

      expect(ws.sendMessage).toHaveBeenCalledExactlyOnceWith('conv-1', {
        content: 'photos',
        attachments: [{ mediaFileId: 'f1' }, { mediaFileId: 'f2' }],
      })
      expect(preview(wrapper).exists()).toBe(false)
    })

    it('puts the attachments back when sending fails', async () => {
      await openConversation()
      mediaFileService.uploadFile
        .mockResolvedValueOnce(uploaded('f1'))
        .mockResolvedValueOnce(uploaded('f2'))
      const ack = mockSend()
      const wrapper = mountBar()
      await chooseFile(wrapper, photo())
      await pressEnter(wrapper)
      // A new file attached while the first message waits for its ack
      await chooseFile(wrapper, photo())

      ack.reject(new Error('Connection closed'))
      await flushPromises()

      expect(
        (preview(wrapper).props('files') as { mediaFileId: string }[]).map((f) => f.mediaFileId)
      ).toEqual(['f1', 'f2'])
    })
  })
})
