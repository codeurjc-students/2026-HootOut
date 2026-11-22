import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import UpdateChannelForm from '@/components/channel/UpdateChannelForm.vue'
import { ApiError, ValidationError, type ChatChannel, type ChatServer } from '@/types'

// ---------- mocks ----------

const channelService = vi.hoisted(() => ({
  updateChatChannel: vi.fn(),
  deleteChatChannel: vi.fn(),
}))
vi.mock('@/services/chatChannel/ChatChannelService', () => ({ default: channelService }))

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

function makeChannel(overrides: Partial<ChatChannel> = {}): ChatChannel {
  return {
    id: 'c1',
    serverId: 's1',
    chatConversationId: 'conversation-c1',
    name: 'general',
    createdAt: null,
    modifiedAt: null,
    ...overrides,
  }
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

interface Props {
  channel?: ChatChannel | null
  server?: ChatServer | null
}

// Without arguments the form gets a channel and its server
function mountForm(props: Props = { channel: makeChannel(), server: makeServer() }) {
  return mount(UpdateChannelForm, { props })
}

const input = (wrapper: VueWrapper) => wrapper.find<HTMLInputElement>('input#update-channel-name')
const updateButton = (wrapper: VueWrapper) =>
  wrapper.find<HTMLButtonElement>('button[type="submit"]')
const deleteButton = (wrapper: VueWrapper) =>
  wrapper.find<HTMLButtonElement>('button.button-action-danger')
const alert = (wrapper: VueWrapper) => wrapper.find('[role="alert"]')
const fieldErrors = (wrapper: VueWrapper) =>
  wrapper.findAll('.custom-form-field-error').map((p) => p.text())

async function submit(wrapper: VueWrapper, name?: string) {
  if (name !== undefined) await input(wrapper).setValue(name)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

async function clickDelete(wrapper: VueWrapper) {
  await deleteButton(wrapper).trigger('click')
  await flushPromises()
}

function nameValidationError(status = 400) {
  return new ValidationError(
    [{ propertyName: 'Name', errorCode: 'MinimumLength', errorMessage: 'Name is too short.' }],
    status
  )
}

// ---------- tests ----------

describe('UpdateChannelForm.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
  })

  describe('rendering', () => {
    it('shows the name of the server', () => {
      const wrapper = mountForm()

      expect(wrapper.text()).toContain('Server: My Server')
    })

    it('links the label to the name input', () => {
      const wrapper = mountForm()

      expect(wrapper.find('label').attributes('for')).toBe('update-channel-name')
      expect(input(wrapper).exists()).toBe(true)
    })

    it('fills the input with the name of the channel', () => {
      const wrapper = mountForm()

      expect(input(wrapper).element.value).toBe('general')
      expect(updateButton(wrapper).element.disabled).toBe(false)
      expect(deleteButton(wrapper).element.disabled).toBe(false)
    })

    it('shows the name of the new channel when another channel is passed', async () => {
      const wrapper = mountForm()
      await input(wrapper).setValue('edited but not saved')

      await wrapper.setProps({ channel: makeChannel({ id: 'c2', name: 'random' }) })

      expect(input(wrapper).element.value).toBe('random')
    })

    it('removes the errors when another channel is passed', async () => {
      channelService.updateChatChannel.mockRejectedValue(nameValidationError())
      const wrapper = mountForm()
      await submit(wrapper, 'ge')

      await wrapper.setProps({ channel: makeChannel({ id: 'c2', name: 'random' }) })

      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })

    it.each([
      ['there is no channel', {}],
      ['the channel is null', { channel: null }],
      ['the channel has an empty id', { channel: makeChannel({ id: ' ' }) }],
    ])('disables both buttons when %s', async (_, props: Props) => {
      const wrapper = mountForm(props)
      await input(wrapper).setValue('general')

      expect(updateButton(wrapper).element.disabled).toBe(true)
      expect(deleteButton(wrapper).element.disabled).toBe(true)
    })
  })

  describe('update', () => {
    it.each([
      ['empty', ''],
      ['only spaces', '   '],
    ])('disables the update button, but not delete, when the name is %s', async (_, name) => {
      const wrapper = mountForm()

      await input(wrapper).setValue(name)

      expect(updateButton(wrapper).element.disabled).toBe(true)
      expect(deleteButton(wrapper).element.disabled).toBe(false)
    })

    it('updates the channel with the trimmed name', async () => {
      channelService.updateChatChannel.mockResolvedValue(makeChannel({ name: 'news' }))
      const wrapper = mountForm()

      await submit(wrapper, '  news  ')

      expect(channelService.updateChatChannel).toHaveBeenCalledExactlyOnceWith({
        id: 'c1',
        name: 'news',
      })
    })

    it('emits success with the updated channel and keeps its name in the input', async () => {
      const updated = makeChannel({ name: 'news' })
      channelService.updateChatChannel.mockResolvedValue(updated)
      const wrapper = mountForm()

      await submit(wrapper, 'news')

      expect(wrapper.emitted('success')).toEqual([[updated]])
      expect(input(wrapper).element.value).toBe('news')
    })

    it('does nothing when the name is only spaces', async () => {
      const wrapper = mountForm()

      await submit(wrapper, '   ')

      expect(channelService.updateChatChannel).not.toHaveBeenCalled()
    })

    it('does nothing when there is no channel', async () => {
      const wrapper = mountForm({ server: makeServer() })

      await submit(wrapper, 'news')

      expect(channelService.updateChatChannel).not.toHaveBeenCalled()
    })

    it('disables both buttons while the request is in progress', async () => {
      const request = deferred<ChatChannel>()
      channelService.updateChatChannel.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await submit(wrapper, 'news')
      expect(updateButton(wrapper).element.disabled).toBe(true)
      expect(deleteButton(wrapper).element.disabled).toBe(true)

      request.resolve(makeChannel({ name: 'news' }))
      await flushPromises()
      expect(updateButton(wrapper).element.disabled).toBe(false)
      expect(deleteButton(wrapper).element.disabled).toBe(false)
    })

    it('sends a single request when it is submitted twice in a row', async () => {
      const request = deferred<ChatChannel>()
      channelService.updateChatChannel.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await input(wrapper).setValue('news')
      await wrapper.find('form').trigger('submit')
      await wrapper.find('form').trigger('submit')
      request.resolve(makeChannel({ name: 'news' }))
      await flushPromises()

      expect(channelService.updateChatChannel).toHaveBeenCalledTimes(1)
    })
  })

  describe('update errors', () => {
    it('shows the message of an API error below 500', async () => {
      channelService.updateChatChannel.mockRejectedValue(new ApiError(404, 'Channel not found'))
      const wrapper = mountForm()

      await submit(wrapper, 'news')

      expect(alert(wrapper).text()).toBe('Channel not found')
      expect(wrapper.emitted('success')).toBeUndefined()
    })

    it.each([
      ['an API error of 500 or more', new ApiError(503, 'Internal details')],
      ['an unknown error', new Error('Network down')],
      ['a validation error that is not a 400', nameValidationError(422)],
    ])('shows a generic message for %s', async (_, error) => {
      channelService.updateChatChannel.mockRejectedValue(error)
      const wrapper = mountForm()

      await submit(wrapper, 'news')

      expect(alert(wrapper).text()).toBe('Something went wrong. Please try again.')
      expect(fieldErrors(wrapper)).toEqual([])
    })

    it('shows the errors of the name for a validation error and keeps the typed name', async () => {
      channelService.updateChatChannel.mockRejectedValue(nameValidationError())
      const wrapper = mountForm()

      await submit(wrapper, 'ge')

      expect(alert(wrapper).text()).toBe('Please review the form.')
      expect(fieldErrors(wrapper)).toEqual(['Name is too short.'])
      expect(input(wrapper).element.value).toBe('ge')
    })

    it('removes the previous errors when it is submitted again', async () => {
      channelService.updateChatChannel
        .mockRejectedValueOnce(nameValidationError())
        .mockResolvedValueOnce(makeChannel({ name: 'news' }))
      const wrapper = mountForm()

      await submit(wrapper, 'ge')
      await submit(wrapper, 'news')

      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })
  })

  describe('delete', () => {
    it('deletes the channel and emits deleted with the returned id', async () => {
      channelService.deleteChatChannel.mockResolvedValue('c1')
      const wrapper = mountForm()

      await clickDelete(wrapper)

      expect(channelService.deleteChatChannel).toHaveBeenCalledExactlyOnceWith('c1')
      expect(wrapper.emitted('deleted')).toEqual([['c1']])
    })

    it('can delete the channel when the name input is empty', async () => {
      channelService.deleteChatChannel.mockResolvedValue('c1')
      const wrapper = mountForm()
      await input(wrapper).setValue('')

      await clickDelete(wrapper)

      expect(wrapper.emitted('deleted')).toEqual([['c1']])
    })

    it('disables both buttons while the channel is being deleted', async () => {
      const request = deferred<string>()
      channelService.deleteChatChannel.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await clickDelete(wrapper)
      expect(updateButton(wrapper).element.disabled).toBe(true)
      expect(deleteButton(wrapper).element.disabled).toBe(true)

      request.resolve('c1')
      await flushPromises()
      expect(deleteButton(wrapper).element.disabled).toBe(false)
    })

    it('sends a single request when it is clicked twice in a row', async () => {
      const request = deferred<string>()
      channelService.deleteChatChannel.mockReturnValue(request.promise)
      const wrapper = mountForm()

      // Calls the handler twice even if the DOM disables the button in between
      await deleteButton(wrapper).trigger('click')
      deleteButton(wrapper).element.disabled = false
      await deleteButton(wrapper).trigger('click')
      request.resolve('c1')
      await flushPromises()

      expect(channelService.deleteChatChannel).toHaveBeenCalledTimes(1)
      expect(wrapper.emitted('deleted')).toHaveLength(1)
    })

    it('does not update the channel', async () => {
      channelService.deleteChatChannel.mockResolvedValue('c1')
      const wrapper = mountForm()

      await clickDelete(wrapper)

      expect(channelService.updateChatChannel).not.toHaveBeenCalled()
      expect(wrapper.emitted('success')).toBeUndefined()
    })
  })

  describe('delete errors', () => {
    it('shows the message of an API error below 500', async () => {
      channelService.deleteChatChannel.mockRejectedValue(
        new ApiError(403, 'You cannot delete this channel')
      )
      const wrapper = mountForm()

      await clickDelete(wrapper)

      expect(alert(wrapper).text()).toBe('You cannot delete this channel')
      expect(wrapper.emitted('deleted')).toBeUndefined()
      expect(deleteButton(wrapper).element.disabled).toBe(false)
    })

    it.each([
      ['an API error of 500 or more', new ApiError(500, 'Internal details')],
      ['an unknown error', new Error('Network down')],
    ])('shows a generic message for %s', async (_, error) => {
      channelService.deleteChatChannel.mockRejectedValue(error)
      const wrapper = mountForm()

      await clickDelete(wrapper)

      expect(alert(wrapper).text()).toBe('Something went wrong. Please try again.')
    })

    it('removes the errors of a previous update', async () => {
      channelService.updateChatChannel.mockRejectedValue(nameValidationError())
      channelService.deleteChatChannel.mockResolvedValue('c1')
      const wrapper = mountForm()
      await submit(wrapper, 'ge')

      await clickDelete(wrapper)

      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })
  })

  describe('cleanForm', () => {
    it('restores the name of the channel and removes the errors', async () => {
      channelService.updateChatChannel.mockRejectedValue(nameValidationError())
      const wrapper = mountForm()
      await submit(wrapper, 'ge')

      wrapper.vm.cleanForm()
      await flushPromises()

      expect(input(wrapper).element.value).toBe('general')
      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })
  })
})
