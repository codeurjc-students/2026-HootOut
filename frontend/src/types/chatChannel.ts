export interface ChatChannel {
  id: string
  serverId: string
  chatConversationId: string
  name: string
  createdAt: string | null
  modifiedAt: string | null
}

export interface CreateChatChannelRequest {
  name: string
  chatServerId: string
}

export interface UpdateChatChannelRequest {
  id: string
  name: string
}
