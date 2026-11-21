import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import SendFriendRequestForm from '@/components/directConversation/friendRequest/SendFriendRequestForm.vue'
import { ApiError, InvitationStatus, ValidationError, type FriendRequest } from '@/types'

// ---------- mocks ----------

const friendsService = vi.hoisted(() => ({ sendFriendRequest: vi.fn() }))
vi.mock('@/services/friendsService/FriendsService', () => ({ default: friendsService }))

// ---------- helpers ----------

function makeFriendRequest(overrides: Partial<FriendRequest> = {}): FriendRequest {
  return {
    id: 'fr1',
    userId: 'u2',
    username: 'alice',
    invitedBy: 'u1',
    invitedByUsername: 'me',
    status: InvitationStatus.Pending,
    createdAt: '2026-11-21T15:00:00',
    modifiedAt: '2026-11-21T15:00:00',
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

const mountForm = () => mount(SendFriendRequestForm)

const input = (wrapper: VueWrapper) =>
  wrapper.find<HTMLInputElement>('input#friend-request-username')
const submitButton = (wrapper: VueWrapper) =>
  wrapper.find<HTMLButtonElement>('button[type="submit"]')
const alert = (wrapper: VueWrapper) => wrapper.find('[role="alert"]')
const fieldErrors = (wrapper: VueWrapper) =>
  wrapper.findAll('.custom-form-field-error').map((p) => p.text())

async function submit(wrapper: VueWrapper, username: string) {
  await input(wrapper).setValue(username)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

function usernameValidationError(status = 400) {
  return new ValidationError(
    [
      {
        propertyName: 'Username',
        errorCode: 'MinimumLength',
        errorMessage: 'Username is too short.',
      },
      { propertyName: 'Username', errorCode: 'NotFound', errorMessage: 'The user does not exist.' },
    ],
    status
  )
}

// ---------- tests ----------

describe('SendFriendRequestForm.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
  })

  describe('rendering', () => {
    it('links the label to the username input', () => {
      const wrapper = mountForm()

      expect(wrapper.find('label').attributes('for')).toBe('friend-request-username')
      expect(input(wrapper).exists()).toBe(true)
    })

    it('shows the help texts', () => {
      const wrapper = mountForm()

      expect(wrapper.text()).toContain('Please check the username before sending a request.')
      expect(wrapper.text()).toContain('You can revoke a friend request at any time.')
    })

    it('starts with an empty username, no error and the submit button disabled', () => {
      const wrapper = mountForm()

      expect(input(wrapper).element.value).toBe('')
      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
      expect(submitButton(wrapper).element.disabled).toBe(true)
    })
  })

  describe('submit button', () => {
    it('is enabled when there is a username', async () => {
      const wrapper = mountForm()

      await input(wrapper).setValue('alice')

      expect(submitButton(wrapper).element.disabled).toBe(false)
    })

    it('is disabled when the username is only spaces', async () => {
      const wrapper = mountForm()

      await input(wrapper).setValue('   ')

      expect(submitButton(wrapper).element.disabled).toBe(true)
    })
  })

  describe('submit', () => {
    it('sends the friend request with the trimmed username', async () => {
      friendsService.sendFriendRequest.mockResolvedValue(makeFriendRequest())
      const wrapper = mountForm()

      await submit(wrapper, '  alice  ')

      expect(friendsService.sendFriendRequest).toHaveBeenCalledExactlyOnceWith({
        username: 'alice',
      })
    })

    it('emits success with the friend request and empties the username', async () => {
      const friendRequest = makeFriendRequest()
      friendsService.sendFriendRequest.mockResolvedValue(friendRequest)
      const wrapper = mountForm()

      await submit(wrapper, 'alice')

      expect(wrapper.emitted('success')).toEqual([[friendRequest]])
      expect(input(wrapper).element.value).toBe('')
      expect(submitButton(wrapper).element.disabled).toBe(true)
    })

    it.each([
      ['the username is empty', ''],
      ['the username is only spaces', '   '],
    ])('does nothing when %s', async (_, username) => {
      const wrapper = mountForm()

      await submit(wrapper, username)

      expect(friendsService.sendFriendRequest).not.toHaveBeenCalled()
      expect(wrapper.emitted('success')).toBeUndefined()
    })

    it('disables the submit button while the request is in progress', async () => {
      const request = deferred<FriendRequest>()
      friendsService.sendFriendRequest.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await submit(wrapper, 'alice')
      expect(submitButton(wrapper).element.disabled).toBe(true)

      request.resolve(makeFriendRequest())
      await flushPromises()
      await input(wrapper).setValue('bob')
      expect(submitButton(wrapper).element.disabled).toBe(false)
    })

    it('sends a single request when it is submitted twice in a row', async () => {
      const request = deferred<FriendRequest>()
      friendsService.sendFriendRequest.mockReturnValue(request.promise)
      const wrapper = mountForm()

      await input(wrapper).setValue('alice')
      await wrapper.find('form').trigger('submit')
      await wrapper.find('form').trigger('submit')
      request.resolve(makeFriendRequest())
      await flushPromises()

      expect(friendsService.sendFriendRequest).toHaveBeenCalledTimes(1)
      expect(wrapper.emitted('success')).toHaveLength(1)
    })
  })

  describe('errors', () => {
    it('shows the message of an API error below 500', async () => {
      friendsService.sendFriendRequest.mockRejectedValue(
        new ApiError(409, 'You are already friends')
      )
      const wrapper = mountForm()

      await submit(wrapper, 'alice')

      expect(alert(wrapper).text()).toBe('You are already friends')
      expect(wrapper.emitted('success')).toBeUndefined()
    })

    it.each([
      ['an API error of 500 or more', new ApiError(500, 'Internal details')],
      ['an unknown error', new Error('Network down')],
      ['a validation error that is not a 400', usernameValidationError(422)],
    ])('shows a generic message for %s', async (_, error) => {
      friendsService.sendFriendRequest.mockRejectedValue(error)
      const wrapper = mountForm()

      await submit(wrapper, 'alice')

      expect(alert(wrapper).text()).toBe('Something went wrong. Please try again.')
      expect(fieldErrors(wrapper)).toEqual([])
    })

    it('shows the errors of the username below the input for a validation error', async () => {
      friendsService.sendFriendRequest.mockRejectedValue(usernameValidationError())
      const wrapper = mountForm()

      await submit(wrapper, 'al')

      expect(alert(wrapper).text()).toBe('Please review the form.')
      expect(fieldErrors(wrapper)).toEqual(['Username is too short.', 'The user does not exist.'])
    })

    it('keeps the username the user typed when the request fails', async () => {
      friendsService.sendFriendRequest.mockRejectedValue(new ApiError(404, 'User not found'))
      const wrapper = mountForm()

      await submit(wrapper, 'alice')

      expect(input(wrapper).element.value).toBe('alice')
      expect(submitButton(wrapper).element.disabled).toBe(false)
    })

    it('removes the previous errors when it is submitted again', async () => {
      friendsService.sendFriendRequest
        .mockRejectedValueOnce(usernameValidationError())
        .mockResolvedValueOnce(makeFriendRequest())
      const wrapper = mountForm()

      await submit(wrapper, 'al')
      await submit(wrapper, 'alice')

      expect(alert(wrapper).exists()).toBe(false)
      expect(fieldErrors(wrapper)).toEqual([])
    })
  })
})
