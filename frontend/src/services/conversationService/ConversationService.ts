import type { DirectConversation } from '@/types'
import { get } from '../common/api/ApiService'

const ConversationService = {
  getDirectConversationById: async function (
    directConversationId: string
  ): Promise<DirectConversation> {
    return await get(`chatConversations/direct/${directConversationId}`)
  },
}

export default ConversationService
