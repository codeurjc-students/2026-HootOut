import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import ReceivedFriendRequests from '@/components/user/settings/friendRequests/ReceivedFriendRequests.vue'
import { dateTimeMapper } from '@/services/common/utils'
import {
  InvitationStatus,
  InvitationStatusMapper,
  type FriendRequest,
  type PageResult,
} from '@/types'

// ---------- mocks ----------

const friendsService = vi.hoisted(() => ({
  getReceivedFriendRequests: vi.fn(),
  updateFriendRequest: vi.fn(),
}))
vi.mock('@/services/friendsService/FriendsService', () => ({ default: friendsService }))

// ---------- helpers ----------

// A friend request that "user-<id>" sent to the current user
function friendRequest(id: number): FriendRequest {
  return {
    id: `request-${id}`,
    userId: 'me-id',
    username: 'me',
    invitedBy: `user-id-${id}`,
    invitedByUsername: `user-${id}`,
    status: InvitationStatus.Pending,
    createdAt: '2026-11-05T10:42:00Z',
    modifiedAt: '2026-11-06T09:15:00Z',
  }
}

function page(
  items: FriendRequest[],
  next: string | null = null,
  total = items.length
): PageResult<FriendRequest> {
  return { items, next, total, pageSize: 10 }
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

async function mountReceived() {
  const wrapper = mount(ReceivedFriendRequests, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
  await flushPromises()
  return wrapper
}

type Action = 'Accept' | 'Reject'

const rows = (wrapper: VueWrapper) => wrapper.findAll('tbody tr')
const cells = (wrapper: VueWrapper, row: number) => rows(wrapper)[row]!.findAll('td')
const button = (wrapper: VueWrapper, row: number, text: Action) =>
  rows(wrapper)
    [row]!.findAll('button')
    .find((b) => b.text() === text)!
const nextButton = (wrapper: VueWrapper) =>
  wrapper.findAll('button').find((b) => b.text() === 'chevron_right')!

async function click(wrapper: VueWrapper, row: number, text: Action) {
  await button(wrapper, row, text).trigger('click')
  await flushPromises()
}

// ---------- tests ----------

describe('ReceivedFriendRequests.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    friendsService.getReceivedFriendRequests.mockResolvedValue(
      page([friendRequest(1), friendRequest(2)])
    )
    friendsService.updateFriendRequest.mockResolvedValue(undefined)
    // Dates are shown in the local time zone: fix it so the tests behave the same everywhere
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('header', () => {
    it('links back to the friend requests page with the page title', async () => {
      const wrapper = await mountReceived()

      const link = wrapper.findComponent(RouterLinkStub)
      expect(link.props('to')).toEqual({ name: 'friendRequests' })
      expect(link.text()).toContain('Received Friend Requests:')
    })

    it('shows the title only once on mobile (in the back link)', async () => {
      const wrapper = await mountReceived()

      expect(wrapper.find('h2').exists()).toBe(false)
      expect(wrapper.find('h1').text()).toBe('Received Friend Requests:')
    })
  })

  describe('table', () => {
    it('requests the first page of received friend requests sorted by who sent them', async () => {
      await mountReceived()

      expect(friendsService.getReceivedFriendRequests).toHaveBeenCalledExactlyOnceWith({
        next: null,
        descending: true,
        pageSize: 10,
        sortBy: 'invitedbyusername',
      })
    })

    it('shows the columns and an Actions column', async () => {
      const wrapper = await mountReceived()

      expect(wrapper.findAll('th').map((th) => th.text())).toEqual([
        expect.stringContaining('Username'),
        'Status',
        'Invited Date',
        'Last Update',
        'Actions',
      ])
    })

    it('shows who sent the friend request, the status name and the formatted dates', async () => {
      const wrapper = await mountReceived()

      const [username, status, invitedDate, lastUpdate] = cells(wrapper, 0).map((td) => td.text())
      expect(username).toBe('user-1')
      expect(status).toBe(InvitationStatusMapper(InvitationStatus.Pending))
      expect(invitedDate).toBe(dateTimeMapper('2026-11-05T10:42:00Z'))
      expect(lastUpdate).toBe(dateTimeMapper('2026-11-06T09:15:00Z'))
    })

    it('shows the Accept and Reject buttons in each row', async () => {
      const wrapper = await mountReceived()

      for (const index of [0, 1]) {
        expect(button(wrapper, index, 'Accept')).toBeDefined()
        expect(button(wrapper, index, 'Reject')).toBeDefined()
      }
    })
  })

  describe.each([
    ['Accept', InvitationStatus.Accepted],
    ['Reject', InvitationStatus.Rejected],
  ] as const)('%s', (action, status) => {
    it('updates the friend request of its row', async () => {
      const wrapper = await mountReceived()

      await click(wrapper, 1, action)

      expect(friendsService.updateFriendRequest).toHaveBeenCalledExactlyOnceWith({
        id: 'request-2',
        status,
      })
    })

    it('reloads the current page of the table', async () => {
      friendsService.getReceivedFriendRequests
        .mockResolvedValueOnce(page([friendRequest(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([friendRequest(11), friendRequest(12)], null, 15))
        .mockResolvedValueOnce(page([friendRequest(12)], null, 14))
      const wrapper = await mountReceived()
      await nextButton(wrapper).trigger('click')
      await flushPromises()

      await click(wrapper, 0, action)

      expect(friendsService.getReceivedFriendRequests).toHaveBeenCalledTimes(3)
      expect(friendsService.getReceivedFriendRequests).toHaveBeenLastCalledWith(
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(rows(wrapper)).toHaveLength(1)
      expect(cells(wrapper, 0)[0]!.text()).toBe('user-12')
    })

    it('keeps the friend request and does not reload when the request fails', async () => {
      friendsService.updateFriendRequest.mockRejectedValue(new Error('Network down'))
      const wrapper = await mountReceived()

      await click(wrapper, 0, action)

      expect(friendsService.getReceivedFriendRequests).toHaveBeenCalledTimes(1)
      expect(rows(wrapper)).toHaveLength(2)
      expect(button(wrapper, 0, action).attributes('disabled')).toBeUndefined()
    })
  })

  describe('while a friend request is being updated', () => {
    it('disables both buttons of its row only', async () => {
      const request = deferred<unknown>()
      friendsService.updateFriendRequest.mockReturnValue(request.promise)
      const wrapper = await mountReceived()

      await click(wrapper, 0, 'Accept')

      expect(button(wrapper, 0, 'Accept').attributes('disabled')).toBeDefined()
      expect(button(wrapper, 0, 'Reject').attributes('disabled')).toBeDefined()
      expect(button(wrapper, 1, 'Accept').attributes('disabled')).toBeUndefined()

      request.resolve(undefined)
      await flushPromises()
      expect(button(wrapper, 0, 'Accept').attributes('disabled')).toBeUndefined()
    })

    it('ignores a second click on the same row', async () => {
      const request = deferred<unknown>()
      friendsService.updateFriendRequest.mockReturnValue(request.promise)
      const wrapper = await mountReceived()

      // Calls the handler twice even if the DOM disables the button in between
      await button(wrapper, 0, 'Accept').trigger('click')
      button(wrapper, 0, 'Reject').element.removeAttribute('disabled')
      await button(wrapper, 0, 'Reject').trigger('click')
      request.resolve(undefined)
      await flushPromises()

      expect(friendsService.updateFriendRequest).toHaveBeenCalledExactlyOnceWith({
        id: 'request-1',
        status: InvitationStatus.Accepted,
      })
    })

    it('lets another row be updated at the same time', async () => {
      friendsService.updateFriendRequest.mockReturnValue(new Promise(() => {}))
      const wrapper = await mountReceived()

      await click(wrapper, 0, 'Accept')
      await click(wrapper, 1, 'Reject')

      expect(friendsService.updateFriendRequest).toHaveBeenCalledTimes(2)
    })
  })
})
