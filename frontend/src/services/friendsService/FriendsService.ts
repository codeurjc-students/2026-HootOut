import type {
  FriendRequest,
  PageRequest,
  PageResult,
  SendFriendRequest,
  updateFriendrequest,
  UserFriendResponse,
} from '@/types'
import { get, post, put } from '../common/api/ApiService'

const FriendsService = {
  getUserFriends: async function (
    pageRequest: PageRequest
  ): Promise<PageResult<UserFriendResponse>> {
    return await get(`friends`, pageRequest)
  },

  sendFriendRequest: async function (request: SendFriendRequest): Promise<FriendRequest> {
    return await post('friends/requests', request)
  },

  updateFriendRequest: async function (request: updateFriendrequest) {
    return await put('friends/requests', request)
  },

  getReceivedFriendRequests: async function (
    pageRequest: PageRequest
  ): Promise<PageResult<FriendRequest>> {
    return await get('friends/requests/received', pageRequest)
  },

  getSentFriendRequests: async function (
    pageRequest: PageRequest
  ): Promise<PageResult<FriendRequest>> {
    return await get('friends/requests/sent', pageRequest)
  },
}

export default FriendsService
