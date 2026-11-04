import type {
  ChatServer,
  CreateChatServerRequest,
  InviteServerMemberRequest,
  PageRequest,
  PageResult,
  ServerInvitation,
  ServerMember,
  UpdateServerInvitationRequest,
} from '@/types'
import { get, post, put, type Query } from '../common/api/ApiService'

const ChatServerService = {
  createNewServer: async function (request: CreateChatServerRequest): Promise<ChatServer> {
    return await post('chatServers', request)
  },

  getServers: async function (): Promise<ChatServer[]> {
    return await get('chatServers/joined')
  },

  getServerById: async function (serverId: string): Promise<ChatServer> {
    return await get(`chatServers/${serverId}`)
  },

  getServerInvitations: async function (
    serverId: string,
    pageRequest: PageRequest
  ): Promise<PageResult<ServerInvitation>> {
    return await get(`chatServers/${serverId}/invitations`, pageRequest)
  },

  getUserServerInvitations: async function (
    pageRequest: PageRequest
  ): Promise<PageResult<ServerInvitation>> {
    return await get(`chatServers/invitations`, pageRequest)
  },

  inviteUser: async function (request: InviteServerMemberRequest): Promise<ServerInvitation> {
    return await post('chatservers/invitation', request)
  },

  updateServerInvitation: async function (
    request: UpdateServerInvitationRequest
  ): Promise<string | undefined> {
    return await put('chatservers/invitation', request)
  },

  getServerMembers: async function (
    serverid: string,
    pageRequest: PageRequest
  ): Promise<PageResult<ServerMember>> {
    return await get(`chatservers/${serverid}/members`, pageRequest)
  },
}

export default ChatServerService
