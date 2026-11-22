import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import CreateChannelForm from '@/components/channel/CreateChannelForm.vue'
import { ApiError, ValidationError, type ChatChannel, type ChatServer } from '@/types'

// ---------- mocks ----------

const channelService = vi.hoisted(() => ({ createNewChannel: vi.fn() }))
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

// Without arguments the form gets a server; pass { server: ... } to test other cases
function mountForm(props: { server?: ChatServer | null } = { server: makeServer() }) {
  return mount(CreateChannelForm, { props })
}

const input = (wrapper: VueWrapper) => wrapper.find<HTMLInputElement>('input#create-channel-name')
const submitButton = (wrapper: VueWrapper) =>
  wrapper.find<HTMLButtonElement>('button[type="submit"]')
const alert = (wrapper: VueWrapper) => wrapper.find('[role="alert"]')
const fieldErrors = (wrapper: VueWrapper) =>
  wrapper.findAll('.custom-form-field-error').map((p) => p.text())

async function submit(wrapper: VueWrapper, name: string) {
  await input(wrapper).setValue(name)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

function nameValidationError(status = 400) {
  return new ValidationError(
    [
      { propertyName: 'Name', errorCode: 'MinimumLength', errorMessage: 'Name is too short.' },
      { propertyName: 'Name', errorCode: 'Duplicated', errorMessage: 'Name already exists.' },
    ],
    status
  )
}

// ---------- tests ----------

describe('CreateChannelForm.vue', () => {
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

      expect(wrapper.find('label').attributes('for')).toBe('create-channel-name')
      expect(input(wrapper).exists()).toBe(true)
    })

    it('starts with an empty name, no error and the submit button disabled', () => {
      const wrapper = mountForm()

      expect(input(wrapper).element.value).toBe('')
      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
      expect(submitButton(wrapper).element.disabled).toBe(true)
    })
  })

  describe('submit button', () => {
    it('is enabled when there is a name and a server', async () => {
      const wrapper = mountForm()

      await input(wrapper).setValue('general')

      expect(submitButton(wrapper).element.disabled).toBe(false)
    })

    it('is disabled when the name is only spaces', async () => {
      const wrapper = mountForm()

      await input(wrapper).setValue('   ')

      expect(submitButton(wrapper).element.disabled).toBe(true)
    })

    it.each([
      ['there is no server', undefined],
      ['the server is null', null],
      ['the server has an empty id', makeServer({ id: '  ' })],
    ])('is disabled when %s', async (_, server) => {
      const wrapper = mountForm({ server })

      await input(wrapper).setValue('general')

      expect(submitButton(wrapper).element.disabled).toBe(true)
    })
  })

  describe('submit', () => {
    it('creates the channel in the server with the trimmed name', async () => {
      channelService.createNewChannel.mockResolvedValue(makeChannel())
      const wrapper = mountForm()

      await submit(wrapper, '  general  ')

      expect(channelService.createNewChannel).toHaveBeenCalledExactlyOnceWith({
        name: 'general',
        chatServerId: 's1',
      })
    })

    it('emits success with the created channel and empties the name', async () => {
      const created = makeChannel()
      channelService.createNewChannel.mockResolvedValue(created)
      const wrapper = mountForm()

      await submit(wrapper, 'general')

      expect(wrapper.emitted('success')).toEqual([[created]])
      expect(input(wrapper).element.value).toBe('')
    })

    it.each([
      ['the name is empty', ''],
      ['the name is only spaces', '   '],
    ])('does nothing when %s', async (_, name) => {
      const wrapper = mountForm()

      await submit(wrapper, name)

      expect(channelService.createNewChannel).not.toHaveBeenCalled()
      expect(wrapper.emitted('success')).toBeUndefined()
    })

    it('does nothing when there is no server', async () => {
      const wrapper = mountForm({})

      await submit(wrapper, 'general')

      expect(channelService.createNewChannel).not.toHaveBeenCalled()
    })

    it('disables the submit button while the request is in progress', async () => {
      const request = deferred<ChatChannel>()
      channelService.createNewChannel.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await submit(wrapper, 'general')
      expect(submitButton(wrapper).element.disabled).toBe(true)

      request.resolve(makeChannel())
      await flushPromises()
      await input(wrapper).setValue('random')
      expect(submitButton(wrapper).element.disabled).toBe(false)
    })

    it('sends a single request when it is submitted twice in a row', async () => {
      const request = deferred<ChatChannel>()
      channelService.createNewChannel.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await input(wrapper).setValue('general')
      await wrapper.find('form').trigger('submit')
      await wrapper.find('form').trigger('submit')
      request.resolve(makeChannel())
      await flushPromises()

      expect(channelService.createNewChannel).toHaveBeenCalledTimes(1)
      expect(wrapper.emitted('success')).toHaveLength(1)
    })
  })

  describe('errors', () => {
    it('shows the message of an API error below 500', async () => {
      channelService.createNewChannel.mockRejectedValue(
        new ApiError(409, 'The channel already exists')
      )
      const wrapper = mountForm()

      await submit(wrapper, 'general')

      expect(alert(wrapper).text()).toBe('The channel already exists')
      expect(wrapper.emitted('success')).toBeUndefined()
    })

    it.each([
      ['an API error of 500 or more', new ApiError(500, 'Internal details')],
      ['an unknown error', new Error('Network down')],
      ['a validation error that is not a 400', nameValidationError(422)],
    ])('shows a generic message for %s', async (_, error) => {
      channelService.createNewChannel.mockRejectedValue(error)
      const wrapper = mountForm()

      await submit(wrapper, 'general')

      expect(alert(wrapper).text()).toBe('Something went wrong. Please try again.')
      expect(fieldErrors(wrapper)).toEqual([])
    })

    it('shows the errors of the name once, below the input, for a validation error', async () => {
      channelService.createNewChannel.mockRejectedValue(nameValidationError())
      const wrapper = mountForm()

      await submit(wrapper, 'ge')

      expect(alert(wrapper).text()).toBe('Please review the form.')
      expect(fieldErrors(wrapper)).toEqual(['Name is too short.', 'Name already exists.'])
    })

    it('keeps the name the user typed when the request fails', async () => {
      channelService.createNewChannel.mockRejectedValue(
        new ApiError(409, 'The channel already exists')
      )
      const wrapper = mountForm()

      await submit(wrapper, 'general')

      expect(input(wrapper).element.value).toBe('general')
      expect(submitButton(wrapper).element.disabled).toBe(false)
    })

    it('removes the previous errors when it is submitted again', async () => {
      channelService.createNewChannel
        .mockRejectedValueOnce(nameValidationError())
        .mockResolvedValueOnce(makeChannel())
      const wrapper = mountForm()

      await submit(wrapper, 'ge')
      await submit(wrapper, 'general')

      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })
  })

  describe('cleanForm', () => {
    it('empties the name and removes the errors', async () => {
      channelService.createNewChannel.mockRejectedValue(nameValidationError())
      const wrapper = mountForm()
      await submit(wrapper, 'ge')

      wrapper.vm.cleanForm()
      await flushPromises()

      expect(input(wrapper).element.value).toBe('')
      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })
  })
})
