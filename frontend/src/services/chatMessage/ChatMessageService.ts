import { get } from '@/services/common/api/ApiService'
import type { ChatMessage, PageResult } from '@/types'

const ChatMessageService = {
  getMessagesByConversationId: async function (
    chatConversationId: string,
    next: string | undefined | null = undefined,
    pageSize: number = 20
  ): Promise<PageResult<ChatMessage>> {
    return await get('chatMessages', { chatConversationId, next, pageSize })
  },
}

export default ChatMessageService
