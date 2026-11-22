import type { InvitationStatus } from './chatServer'
import type { UserInfo } from './userInfo'

export interface FriendRelation {
  id: string
  friend: UserInfo
  chatConversationId: string
  createdAt: string
  modifiedAt: string
}

export interface FriendRequest {
  id: string
  userId?: string | null
  username?: string | null
  invitedBy?: string | null
  invitedByUsername?: string | null
  status: InvitationStatus
  createdAt: string
  modifiedAt: string
}

export interface SendFriendRequest {
  username: string
}

export interface updateFriendrequest {
  id: string
  status: InvitationStatus
}

export interface UserFriendResponse {
  friendRelationId: string
  friend: UserInfo
  chatConversationId: string
  directConversationId: string
}
