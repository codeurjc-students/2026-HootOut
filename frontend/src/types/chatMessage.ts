import type { UserInfo } from './userInfo'

export interface ChatMessage {
  id: string
  author: UserInfo
  content: string
  attachments: MessageAttachment[]
  createdAt: string | null
  modifiedAt: string | null
}

export interface MessageAttachment {
  id?: string
  messageId?: string
  mediaFileId: string
  contentType?: string
  fileName?: string
  url?: string
  downloadUrl?: string
}

export interface CreateChatMessage {
  content: string
}
