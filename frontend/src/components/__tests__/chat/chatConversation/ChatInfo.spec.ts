import { describe, it, expect } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import ChatInfo from '@/components/chat/chatConversation/ChatInfo.vue'

function mountChatInfo(
  chatName = 'general',
  backLink = { name: 'server', params: { serverId: 's1' } }
) {
  return mount(ChatInfo, {
    props: { chatName, backLink },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
}

describe('ChatInfo.vue', () => {
  it('shows the chat name in a back link (mobile) that goes to the given route', () => {
    const wrapper = mountChatInfo('general', { name: 'server', params: { serverId: 's1' } })

    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.props('to')).toEqual({ name: 'server', params: { serverId: 's1' } })
    expect(link.text()).toContain('general')
  })

  it('shows the chat name as a title outside the back link (desktop)', () => {
    const wrapper = mountChatInfo('general')

    const title = wrapper.find('.chat-info > .font-semibold')
    expect(title.text()).toBe('general')
    expect(title.classes()).toEqual(expect.arrayContaining(['hidden', 'md:block']))
  })

  it('updates when the chat changes', async () => {
    const wrapper = mountChatInfo('general')

    await wrapper.setProps({ chatName: 'alice', backLink: { name: 'directConversation' } })

    expect(wrapper.find('.chat-info > .font-semibold').text()).toBe('alice')
    expect(wrapper.findComponent(RouterLinkStub).props('to')).toEqual({
      name: 'directConversation',
    })
  })
})
