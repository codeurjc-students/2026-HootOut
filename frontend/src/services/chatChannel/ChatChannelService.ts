import type { ChatChannel, CreateChatChannelRequest, UpdateChatChannelRequest } from '@/types'
import { deleteHTTP, get, post, put } from '../common/api/ApiService'

const channelsUrl = 'chatchannels'

const ChatChannelService = {
  getChannelsByServerId: async function (serverId: string): Promise<Array<ChatChannel>> {
    return await get(`${channelsUrl}/getByServerId`, { serverId })
  },

  createNewChannel: async function (request: CreateChatChannelRequest): Promise<ChatChannel> {
    return await post(channelsUrl, request)
  },

  updateChatChannel: async function (request: UpdateChatChannelRequest): Promise<ChatChannel> {
    return await put(channelsUrl, request)
  },

  deleteChatChannel: async function (id: string): Promise<string> {
    return await deleteHTTP(`${channelsUrl}/${id}`)
  },
}

export default ChatChannelService
