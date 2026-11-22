import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import FriendsBar from '@/components/directConversation/FriendsBar.vue'
import { InvitationStatus, type FriendRequest } from '@/types'

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

// The form and the list have their own tests: the form's events are emitted directly
const SendFriendRequestFormStub = defineComponent({
  name: 'SendFriendRequestForm',
  emits: ['success'],
  setup: () => () => h('form', { class: 'send-friend-request-form-stub' }),
})
const FriendsListStub = defineComponent({
  name: 'FriendsList',
  setup: () => () => h('div', { class: 'friends-list-stub' }),
})

// ---------- helpers ----------

function makeFriendRequest(): FriendRequest {
  return {
    id: 'fr1',
    userId: 'u2',
    username: 'alice',
    status: InvitationStatus.Pending,
    createdAt: '2026-11-21T15:00:00',
    modifiedAt: '2026-11-21T15:00:00',
  }
}

function mountFriendsBar() {
  return mount(FriendsBar, {
    global: {
      stubs: {
        Modal: ModalStub,
        SendFriendRequestForm: SendFriendRequestFormStub,
        FriendsList: FriendsListStub,
      },
    },
  })
}

const sendRequestButton = (wrapper: VueWrapper) =>
  wrapper.find('button[aria-label="Send friend request"]')

// ---------- tests ----------

describe('FriendsBar.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('shows the "Friends" title', () => {
    const wrapper = mountFriendsBar()

    expect(wrapper.find('h2').text()).toBe('Friends')
  })

  it('has a labelled button to send a friend request', () => {
    const wrapper = mountFriendsBar()

    expect(sendRequestButton(wrapper).exists()).toBe(true)
    expect(sendRequestButton(wrapper).attributes('type')).toBe('button')
  })

  it('shows the list of friends', () => {
    const wrapper = mountFriendsBar()

    expect(wrapper.findComponent(FriendsListStub).exists()).toBe(true)
  })

  it('puts the friend request form in a modal titled "Send Friend Request"', () => {
    const wrapper = mountFriendsBar()

    expect(wrapper.find('.modal-stub h3').text()).toBe('Send Friend Request')
    expect(wrapper.find('.modal-stub').findComponent(SendFriendRequestFormStub).exists()).toBe(true)
  })

  it('opens the modal when the button is clicked', async () => {
    const wrapper = mountFriendsBar()

    await sendRequestButton(wrapper).trigger('click')

    expect(modal.open).toHaveBeenCalledTimes(1)
    expect(modal.close).not.toHaveBeenCalled()
  })

  it('closes the modal when the friend request is sent', async () => {
    const wrapper = mountFriendsBar()
    await sendRequestButton(wrapper).trigger('click')

    wrapper.findComponent(SendFriendRequestFormStub).vm.$emit('success', makeFriendRequest())

    expect(modal.close).toHaveBeenCalledTimes(1)
  })
})
