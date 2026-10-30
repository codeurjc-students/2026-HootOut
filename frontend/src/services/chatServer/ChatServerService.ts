import type { ChatServer, CreateChatServerRequest } from '@/types'
import { get, post } from '../common/api/ApiService'

const ChatServerService = {
  createNewServer: async function (request: CreateChatServerRequest): Promise<ChatServer> {
    return post('chatServers', request)
  },

  getServers: async function (): Promise<ChatServer[]> {
    return get('chatServers/all')
  },
}

export default ChatServerService
