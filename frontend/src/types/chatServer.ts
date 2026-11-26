import type { ChatChannel } from './chatChannel'

export interface Picture {
  mediaFileId: string
  fileName: string
  url: string
}

export interface ChatServer {
  id: string
  name: string
  description?: string | null
  channels: ChatChannel[]
  profilePicture?: Picture | undefined
  createdAt: string | null
  modifiedAt: string | null
}

export interface CreateChatServerRequest {
  name: string
  description: string
  profilePictureId?: string | undefined
}

export enum InvitationStatus {
  Pending = 0,
  Accepted = 10,
  Rejected = 20,
  Revoked = 30,
}

const InvitationStatusDict: Record<InvitationStatus, string> = {
  0: 'Pending',
  10: 'Accepted',
  20: 'Rejected',
  30: 'Revoked',
}

export const InvitationStatusMapper = (status: InvitationStatus): string =>
  InvitationStatusDict[status]

export interface InviteServerMemberRequest {
  chatServerId: string
  username: string
}

export interface UpdateServerInvitationRequest {
  id: string
  status: InvitationStatus
}

export interface ServerInvitation {
  id: string
  chatServerId: string
  chatServerName: string
  userId: string
  username: string
  status: InvitationStatus
  createdAt: string
  modifiedAt: string
}

export interface ServerMember {
  id: string
  username: string
  userId: string
  profilePicture?: Picture | undefined
  createdAt: string
}

// Online members of a server: items is capped by the backend, total is the real number online
export interface OnlineServerMembers {
  items: ServerMember[]
  total: number
}
