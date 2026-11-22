import type { UserInfo } from '@/types'

export interface DirectConversation {
  id: string
  chatConversationId: string
  user1: UserInfo
  user2: UserInfo
}
