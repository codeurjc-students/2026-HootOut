import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub, type VueWrapper } from '@vue/test-utils'
import SentFriendRequests from '@/components/user/settings/friendRequests/SentFriendRequests.vue'
import { dateTimeMapper } from '@/services/common/utils'
import {
  InvitationStatus,
  InvitationStatusMapper,
  type FriendRequest,
  type PageResult,
} from '@/types'

// ---------- mocks ----------

const friendsService = vi.hoisted(() => ({
  getSentFriendRequests: vi.fn(),
  updateFriendRequest: vi.fn(),
}))
vi.mock('@/services/friendsService/FriendsService', () => ({ default: friendsService }))

// ---------- helpers ----------

// A friend request sent by the current user to "user-<id>"
function friendRequest(id: number): FriendRequest {
  return {
    id: `request-${id}`,
    userId: `user-id-${id}`,
    username: `user-${id}`,
    invitedBy: 'me-id',
    invitedByUsername: 'me',
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

async function mountSent() {
  const wrapper = mount(SentFriendRequests, { global: { stubs: { RouterLink: RouterLinkStub } } })
  await flushPromises()
  return wrapper
}

const rows = (wrapper: VueWrapper) => wrapper.findAll('tbody tr')
const cells = (wrapper: VueWrapper, row: number) => rows(wrapper)[row]!.findAll('td')
const revokeButton = (wrapper: VueWrapper, row: number) =>
  rows(wrapper)
    [row]!.findAll('button')
    .find((b) => b.text() === 'Revoke')!
const nextButton = (wrapper: VueWrapper) =>
  wrapper.findAll('button').find((b) => b.text() === 'chevron_right')!

async function clickRevoke(wrapper: VueWrapper, row: number) {
  await revokeButton(wrapper, row).trigger('click')
  await flushPromises()
}

// ---------- tests ----------

describe('SentFriendRequests.vue', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    friendsService.getSentFriendRequests.mockResolvedValue(
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
      const wrapper = await mountSent()

      const link = wrapper.findComponent(RouterLinkStub)
      expect(link.props('to')).toEqual({ name: 'friendRequests' })
      expect(link.text()).toContain('Sent Friend Requests:')
    })

    it('shows the title only once on mobile (in the back link)', async () => {
      const wrapper = await mountSent()

      expect(wrapper.find('h2').exists()).toBe(false)
      expect(wrapper.find('h1').text()).toBe('Sent Friend Requests:')
    })
  })

  describe('table', () => {
    it('requests the first page of sent friend requests sorted by username', async () => {
      await mountSent()

      expect(friendsService.getSentFriendRequests).toHaveBeenCalledExactlyOnceWith({
        next: null,
        descending: true,
        pageSize: 10,
        sortBy: 'username',
      })
    })

    it('shows the columns and an Actions column', async () => {
      const wrapper = await mountSent()

      expect(wrapper.findAll('th').map((th) => th.text())).toEqual([
        expect.stringContaining('Username'),
        'Status',
        'Invited Date',
        'Last Update',
        'Actions',
      ])
    })

    it('shows the invited user, the status name and the formatted dates', async () => {
      const wrapper = await mountSent()

      const [username, status, invitedDate, lastUpdate] = cells(wrapper, 0).map((td) => td.text())
      expect(username).toBe('user-1')
      expect(status).toBe(InvitationStatusMapper(InvitationStatus.Pending))
      expect(invitedDate).toBe(dateTimeMapper('2026-11-05T10:42:00Z'))
      expect(lastUpdate).toBe(dateTimeMapper('2026-11-06T09:15:00Z'))
    })

    it('shows a Revoke button in each row', async () => {
      const wrapper = await mountSent()

      expect(rows(wrapper).map((_, index) => revokeButton(wrapper, index) !== undefined)).toEqual([
        true,
        true,
      ])
    })

    it('requests the next page with its cursor', async () => {
      friendsService.getSentFriendRequests
        .mockResolvedValueOnce(page([friendRequest(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([friendRequest(11)], null, 15))
      const wrapper = await mountSent()

      await nextButton(wrapper).trigger('click')
      await flushPromises()

      expect(friendsService.getSentFriendRequests).toHaveBeenLastCalledWith(
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(cells(wrapper, 0)[0]!.text()).toBe('user-11')
    })
  })

  describe('revoke', () => {
    it('revokes the friend request of its row', async () => {
      const wrapper = await mountSent()

      await clickRevoke(wrapper, 1)

      expect(friendsService.updateFriendRequest).toHaveBeenCalledExactlyOnceWith({
        id: 'request-2',
        status: InvitationStatus.Revoked,
      })
    })

    it('reloads the current page of the table', async () => {
      friendsService.getSentFriendRequests
        .mockResolvedValueOnce(page([friendRequest(1)], 'cursor-2', 15))
        .mockResolvedValueOnce(page([friendRequest(11), friendRequest(12)], null, 15))
        .mockResolvedValueOnce(page([friendRequest(12)], null, 14))
      const wrapper = await mountSent()
      await nextButton(wrapper).trigger('click')
      await flushPromises()

      await clickRevoke(wrapper, 0)

      expect(friendsService.getSentFriendRequests).toHaveBeenLastCalledWith(
        expect.objectContaining({ next: 'cursor-2' })
      )
      expect(rows(wrapper)).toHaveLength(1)
      expect(cells(wrapper, 0)[0]!.text()).toBe('user-12')
    })

    it('keeps the friend request and does not reload when the request fails', async () => {
      friendsService.updateFriendRequest.mockRejectedValue(new Error('Network down'))
      const wrapper = await mountSent()

      await clickRevoke(wrapper, 0)

      expect(friendsService.getSentFriendRequests).toHaveBeenCalledTimes(1)
      expect(rows(wrapper)).toHaveLength(2)
      expect(revokeButton(wrapper, 0).attributes('disabled')).toBeUndefined()
    })

    it('disables only the button of the row being revoked', async () => {
      const request = deferred<unknown>()
      friendsService.updateFriendRequest.mockReturnValue(request.promise)
      const wrapper = await mountSent()

      await clickRevoke(wrapper, 0)

      expect(revokeButton(wrapper, 0).attributes('disabled')).toBeDefined()
      expect(revokeButton(wrapper, 1).attributes('disabled')).toBeUndefined()

      request.resolve(undefined)
      await flushPromises()
      expect(revokeButton(wrapper, 0).attributes('disabled')).toBeUndefined()
    })

    it('ignores a second click while the friend request is being revoked', async () => {
      const request = deferred<unknown>()
      friendsService.updateFriendRequest.mockReturnValue(request.promise)
      const wrapper = await mountSent()

      // Calls the handler twice even if the DOM disables the button in between
      await revokeButton(wrapper, 0).trigger('click')
      revokeButton(wrapper, 0).element.removeAttribute('disabled')
      await revokeButton(wrapper, 0).trigger('click')
      request.resolve(undefined)
      await flushPromises()

      expect(friendsService.updateFriendRequest).toHaveBeenCalledTimes(1)
    })
  })
})
