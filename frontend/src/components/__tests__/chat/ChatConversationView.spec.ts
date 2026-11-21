import { describe, it, expect } from 'vitest'
import { mount, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import ChatConversationView from '@/components/chat/ChatConversationView.vue'
import ChatInfo from '@/components/chat/chatConversation/ChatInfo.vue'

// ---------- stubs ----------

// The messages and the message bar use the stores: they have their own tests
const ChatMessagesStub = defineComponent({
  name: 'ChatMessages',
  setup: () => () => h('div', { class: 'chat-messages-stub' }),
})
const ChatMessageBarStub = defineComponent({
  name: 'ChatMessageBar',
  props: { chatName: String },
  setup: () => () => h('div', { class: 'chat-message-bar-stub' }),
})

// ---------- helpers ----------

const backLink = { name: 'server', params: { serverId: 's1' } }

function mountView(chatName?: string) {
  return mount(ChatConversationView, {
    props: { chatName, backLink },
    global: {
      stubs: {
        RouterLink: RouterLinkStub,
        ChatMessages: ChatMessagesStub,
        ChatMessageBar: ChatMessageBarStub,
      },
    },
  })
}

const chat = (wrapper: VueWrapper) => wrapper.find('#chat')

// ---------- tests ----------

describe('ChatConversationView.vue', () => {
  describe('without a chat', () => {
    it.each([
      ['no chat name', undefined],
      ['an empty chat name', ''],
    ])('asks to start a chat when there is %s', (_, chatName) => {
      const wrapper = mountView(chatName)

      expect(chat(wrapper).exists()).toBe(false)
      expect(wrapper.text()).toBe('Start a new chat')
      expect(wrapper.findComponent(ChatMessagesStub).exists()).toBe(false)
      expect(wrapper.findComponent(ChatMessageBarStub).exists()).toBe(false)
    })
  })

  describe('with a chat', () => {
    it('shows the chat info, the messages and the message bar', () => {
      const wrapper = mountView('general')

      expect(chat(wrapper).exists()).toBe(true)
      expect(wrapper.text()).not.toContain('Start a new chat')
      expect(wrapper.findComponent(ChatInfo).exists()).toBe(true)
      expect(wrapper.findComponent(ChatMessagesStub).exists()).toBe(true)
      expect(wrapper.findComponent(ChatMessageBarStub).exists()).toBe(true)
    })

    it('passes the chat name and the back link to the chat info', () => {
      const wrapper = mountView('general')

      expect(wrapper.findComponent(ChatInfo).props()).toEqual({ chatName: 'general', backLink })
    })

    it('passes the chat name to the message bar', () => {
      const wrapper = mountView('general')

      expect(wrapper.findComponent(ChatMessageBarStub).props('chatName')).toBe('general')
    })

    it('shows the chat when a chat name arrives later', async () => {
      const wrapper = mountView(undefined)

      await wrapper.setProps({ chatName: 'general' })

      expect(chat(wrapper).exists()).toBe(true)
    })
  })

  it('keeps the classes given by the parent in both cases', async () => {
    const wrapper = mount(ChatConversationView, {
      props: { backLink, chatName: 'test' },
      attrs: { class: 'view-chat' },
      global: {
        stubs: {
          RouterLink: RouterLinkStub,
          ChatMessages: ChatMessagesStub,
          ChatMessageBar: ChatMessageBarStub,
        },
      },
    })
    expect(wrapper.classes()).toContain('view-chat')

    await wrapper.setProps({ chatName: 'general' })
    expect(chat(wrapper).classes()).toContain('view-chat')
  })
})
