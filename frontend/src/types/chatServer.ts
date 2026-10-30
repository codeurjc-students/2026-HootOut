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
