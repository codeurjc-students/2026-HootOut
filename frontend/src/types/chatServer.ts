import type { ChatChannel } from './chatChannel'

export interface ChatServer {
  id: string
  name: string
  description: string
  channels: ChatChannel[]
  createdAt: string | null
  modifiedAt: string | null
}

export interface CreateChatServerRequest {
  name: string
  description: string
}

export enum ServerInvitationStatus {
  Pending = 0,
  Accepted = 10,
  Rejected = 20,
  Revoked = 30,
}

const serverInvitationStatusDict: Record<ServerInvitationStatus, string> = {
  0: 'Pending',
  10: 'Accepted',
  20: 'Rejected',
  30: 'Revoked',
}

export const serverInvitationStatusMapper = (status: ServerInvitationStatus): string =>
  serverInvitationStatusDict[status]

export interface InviteServerMemberRequest {
  chatServerId: string
  username: string
}

export interface UpdateServerInvitationRequest {
  id: string
  status: ServerInvitationStatus
}

export interface ServerInvitation {
  id: string
  chatServerId: string
  chatServerName: string
  userId: string
  username: string
  status: ServerInvitationStatus
  createdAt: string
  modifiedAt: string
}

export interface ServerMember {
  id: string
  username: string
  userId: string
  createdAt: string
}
