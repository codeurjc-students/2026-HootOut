import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import ChannelsList from '@/components/channel/ChannelsList.vue'
import { serverInfoStore } from '@/stores/serverInfo'
import type { ChatChannel, ChatMessage, ChatServer, PageResult } from '@/types'

// ---------- mocks ----------

const mocks = vi.hoisted(() => ({
  channelService: { getChannelsByServerId: vi.fn() },
  messageService: { getMessagesByChannelId: vi.fn() },
  ws: { subscribe: vi.fn(), unsubscribe: vi.fn(), addPendingSubscription: vi.fn() },
}))
vi.mock('@/services/chatChannel/ChatChannelService', () => ({ default: mocks.channelService }))
vi.mock('@/services/chatMessage/ChatMessageService', () => ({ default: mocks.messageService }))
vi.mock('@/services/common/websocketsMesage/WebSocketsMessageService', () => ({
  default: mocks.ws,
}))
vi.mock('@/services/chatServer/ChatServerService', () => ({ default: {} }))

const { channelService, messageService } = mocks

// ---------- stubs ----------

// Modal replaced by a stub that exposes open() and close() spies
const modal = { open: vi.fn(), close: vi.fn() }
const ModalStub = defineComponent({
  name: 'Modal',
  props: { title: { type: String, required: true } },
  setup(props, { expose, slots }) {
    expose(modal)
    return () => h('div', { class: 'modal-stub' }, [h('h3', props.title), slots.body?.()])
  },
})

// UpdateChannelForm replaced by a stub: the tests emit its events directly
const UpdateChannelFormStub = defineComponent({
  name: 'UpdateChannelForm',
  props: { channel: Object, server: Object },
  emits: ['success', 'deleted'],
  setup: () => () => h('form', { class: 'update-channel-form-stub' }),
})

// ---------- helpers ----------

const server: ChatServer = {
  id: 's1',
  name: 'My Server',
  channels: [],
  createdAt: '123',
  modifiedAt: '123',
}

function channel(id: string, name = id): ChatChannel {
  return { id, name, serverId: '123', modifiedAt: '123', createdAt: '123' }
}

function messagesPage(items: ChatMessage[]): PageResult<ChatMessage> {
  return { items, next: null, total: items.length, pageSize: 20 }
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

// Store with the current server, its channels and, optionally, one of them open
async function setUpStore(channels: ChatChannel[], openChannelId?: string) {
  const store = serverInfoStore()
  store.setCurrentServerSettings({ ...server, channels })
  store.setServerChannels(channels)
  if (openChannelId) {
    messageService.getMessagesByChannelId.mockResolvedValueOnce(
      messagesPage([{ id: 'old-message' } as ChatMessage])
    )
    await store.selectChannel(openChannelId)
  }
  vi.clearAllMocks()
  return store
}

async function mountChannelLists(path = '/chat/server/s1') {
  await router.push(path)
  await router.isReady()
  const wrapper = mount(ChannelsList, {
    global: {
      plugins: [pinia, router],
      stubs: { Modal: ModalStub, UpdateChannelForm: UpdateChannelFormStub },
    },
  })
  await flushPromises()
  return wrapper
}

const rows = (wrapper: VueWrapper) => wrapper.findAll('.channel-name-row')
const channelLink = (wrapper: VueWrapper, name: string) =>
  wrapper.findAll('a').find((a) => a.text() === name)!
const editButton = (wrapper: VueWrapper, name: string) =>
  wrapper.find(`button[aria-label="Edit channel ${name}"]`)
const form = (wrapper: VueWrapper) => wrapper.findComponent(UpdateChannelFormStub)

async function emitFromForm(wrapper: VueWrapper, event: 'success' | 'deleted', payload: unknown) {
  form(wrapper).vm.$emit(event, payload)
  await flushPromises()
}

// ---------- tests ----------

describe('ChannelLists.vue', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    router = createTestRouter()
    vi.resetAllMocks()
  })

  describe('channel list', () => {
    it('shows a row with a link for each channel of the server', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')])

      const wrapper = await mountChannelLists()

      expect(rows(wrapper)).toHaveLength(2)
      expect(channelLink(wrapper, 'general').attributes('href')).toBe('/chat/server/s1/channel/c1')
      expect(channelLink(wrapper, 'random').attributes('href')).toBe('/chat/server/s1/channel/c2')
    })

    it('highlights only the open channel', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')], 'c2')

      const wrapper = await mountChannelLists('/chat/server/s1/channel/c2')

      expect(rows(wrapper).map((row) => row.classes().includes('background-light'))).toEqual([
        false,
        true,
      ])
    })

    it('has a labelled edit button for each channel', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')])

      const wrapper = await mountChannelLists()

      expect(editButton(wrapper, 'general').exists()).toBe(true)
      expect(editButton(wrapper, 'random').exists()).toBe(true)
    })

    it('navigates to a channel when its link is clicked', async () => {
      await setUpStore([channel('c1', 'general')])
      const wrapper = await mountChannelLists()

      await channelLink(wrapper, 'general').trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('channel')
      expect(router.currentRoute.value.params).toEqual({ serverId: 's1', channelId: 'c1' })
    })

    it('shows no list, without errors, when there is no current server', async () => {
      // Channels left from a previous server, e.g. when the new server was not found
      serverInfoStore().setServerChannels([channel('c1', 'general')])

      const wrapper = await mountChannelLists('/chat')

      expect(rows(wrapper)).toHaveLength(0)
    })
  })

  describe('editing a channel', () => {
    it('opens the update modal with the channel and the server', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')])
      const wrapper = await mountChannelLists()

      await editButton(wrapper, 'random').trigger('click')

      expect(modal.open).toHaveBeenCalledTimes(1)
      expect(wrapper.findComponent(ModalStub).props('title')).toBe('Update Channel')
      expect(form(wrapper).props('channel')).toEqual(channel('c2', 'random'))
      expect(form(wrapper).props('server')).toMatchObject({ id: 's1' })
    })

    it('closes the modal and shows the new name when the channel is updated', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')])
      const wrapper = await mountChannelLists()
      await editButton(wrapper, 'random').trigger('click')

      await emitFromForm(wrapper, 'success', channel('c2', 'off-topic'))

      expect(modal.close).toHaveBeenCalledTimes(1)
      expect(channelLink(wrapper, 'off-topic').exists()).toBe(true)
      expect(serverInfoStore().channels.map((c) => c.name)).toEqual(['general', 'off-topic'])
    })

    it('stays in the open channel after renaming another one', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')], 'c1')
      const wrapper = await mountChannelLists('/chat/server/s1/channel/c1')
      await editButton(wrapper, 'random').trigger('click')

      await emitFromForm(wrapper, 'success', channel('c2', 'off-topic'))

      expect(router.currentRoute.value.params.channelId).toBe('c1')
      expect(serverInfoStore().currentChatChannel?.id).toBe('c1')
    })

    it('updates the name of the open channel when it is the one renamed', async () => {
      await setUpStore([channel('c1', 'general')], 'c1')
      const wrapper = await mountChannelLists('/chat/server/s1/channel/c1')
      await editButton(wrapper, 'general').trigger('click')

      await emitFromForm(wrapper, 'success', channel('c1', 'announcements'))

      expect(serverInfoStore().currentChatChannel?.name).toBe('announcements')
    })
  })

  describe('deleting a channel', () => {
    it('closes the modal and reloads the channels of the server', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')])
      channelService.getChannelsByServerId.mockResolvedValueOnce([channel('c1', 'general')])
      const wrapper = await mountChannelLists()
      await editButton(wrapper, 'random').trigger('click')

      await emitFromForm(wrapper, 'deleted', 'c2')

      expect(modal.close).toHaveBeenCalledTimes(1)
      expect(channelService.getChannelsByServerId).toHaveBeenCalledWith('s1')
      expect(rows(wrapper)).toHaveLength(1)
      expect(channelLink(wrapper, 'random')).toBeUndefined()
    })

    it('goes to the server page when the deleted channel is the open one', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')], 'c2')
      channelService.getChannelsByServerId.mockResolvedValueOnce([channel('c1', 'general')])
      const wrapper = await mountChannelLists('/chat/server/s1/channel/c2')

      await emitFromForm(wrapper, 'deleted', 'c2')

      expect(router.currentRoute.value.name).toBe('server')
      expect(router.currentRoute.value.params.serverId).toBe('s1')
      expect(serverInfoStore().currentChatChannel).toBeUndefined()
    })

    it('keeps the open channel and reloads its messages when another channel is deleted', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')], 'c1')
      channelService.getChannelsByServerId.mockResolvedValueOnce([channel('c1', 'general')])
      messageService.getMessagesByChannelId.mockResolvedValueOnce(
        messagesPage([{ id: 'm1' } as ChatMessage])
      )
      const wrapper = await mountChannelLists('/chat/server/s1/channel/c1')

      await emitFromForm(wrapper, 'deleted', 'c2')

      expect(router.currentRoute.value.params.channelId).toBe('c1')
      expect(serverInfoStore().currentChatChannel?.id).toBe('c1')
      expect(messageService.getMessagesByChannelId).toHaveBeenCalledWith('c1')
      expect(serverInfoStore().channelMessages).toEqual([{ id: 'm1' }])
    })

    it('only reloads the list when no channel is open', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')])
      channelService.getChannelsByServerId.mockResolvedValueOnce([channel('c1', 'general')])
      const wrapper = await mountChannelLists()

      await emitFromForm(wrapper, 'deleted', 'c2')

      expect(router.currentRoute.value.name).toBe('server')
      expect(messageService.getMessagesByChannelId).not.toHaveBeenCalled()
      expect(serverInfoStore().currentChatChannel).toBeUndefined()
    })

    it('keeps the list, without errors, when reloading the channels fails', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')], 'c1')
      channelService.getChannelsByServerId.mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountChannelLists('/chat/server/s1/channel/c1')

      await emitFromForm(wrapper, 'deleted', 'c2')

      expect(rows(wrapper)).toHaveLength(2)
      expect(router.currentRoute.value.params.channelId).toBe('c1')
    })

    it('does not fail when reloading the messages of the open channel fails', async () => {
      await setUpStore([channel('c1', 'general'), channel('c2', 'random')], 'c1')
      channelService.getChannelsByServerId.mockResolvedValueOnce([channel('c1', 'general')])
      messageService.getMessagesByChannelId.mockRejectedValueOnce(new Error('500'))
      const wrapper = await mountChannelLists('/chat/server/s1/channel/c1')

      await emitFromForm(wrapper, 'deleted', 'c2')

      expect(rows(wrapper)).toHaveLength(1)
    })
  })
})
