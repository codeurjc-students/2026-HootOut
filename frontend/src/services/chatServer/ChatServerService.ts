import type {
  ChatServer,
  CreateChatServerRequest,
  InviteServerMemberRequest,
  PageResult,
  ServerInvitation,
  ServerMember,
  UpdateServerInvitationRequest,
} from '@/types'
import { get, post, put } from '../common/api/ApiService'

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
    next?: string
  ): Promise<PageResult<ServerMember>> {
    return await get(`chatservers/${serverid}/members`, { next })
  },
}

export default ChatServerService
