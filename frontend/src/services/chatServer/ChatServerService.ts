import type {
  ChatServer,
  CreateChatServerRequest,
  InviteServerMemberRequest,
  OnlineServerMembers,
  PageRequest,
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

  getServers: async function (next: string | null): Promise<PageResult<ChatServer>> {
    return await get('chatServers/joined', { next, pageSize: 20 })
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

  // Online members sorted by username. Not paginated: capped by the backend, total is the real count
  getOnlineServerMembers: async function (serverId: string): Promise<OnlineServerMembers> {
    return await get(`chatservers/${serverId}/members/online`)
  },

  // Members that are not online, paginated (sorted by username ascending when sortBy is missing)
  getOfflineServerMembers: async function (
    serverId: string,
    pageRequest: PageRequest
  ): Promise<PageResult<ServerMember>> {
    return await get(`chatservers/${serverId}/members/offline`, pageRequest)
  },
}

export default ChatServerService
