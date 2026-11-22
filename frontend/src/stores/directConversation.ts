import { defineStore } from 'pinia'

import type { UserInfo } from '@/types'
import { ref } from 'vue'
import ConversationService from '@/services/conversationService/ConversationService'

export interface FriendConversation {
  directConversationId: string
  chatConversationId: string
  friend: UserInfo
}

export const directConversationStore = defineStore('directConversation', () => {
  const currentConversation = ref<FriendConversation>()

  // Id of the last conversation requested. A request for another id that finishes later
  // is ignored, so it cannot replace the conversation selected after it
  let requestedId: string | undefined

  // Only the fields of FriendConversation, whatever the object has (e.g. a UserFriendResponse)
  function toFriendConversation(conversation: FriendConversation): FriendConversation {
    return {
      directConversationId: conversation.directConversationId,
      chatConversationId: conversation.chatConversationId,
      friend: conversation.friend,
    }
  }

  /**
   * Loads the conversation of the url (e.g. after a refresh), unless it is already the current one.
   * Returns false when the user cannot open it (the API returns no content) or when another
   * conversation was requested while loading. Errors of the request are thrown
   */
  async function updateDirectConversation(
    directConversationId: string,
    userId: string
  ): Promise<boolean> {
    requestedId = directConversationId
    if (currentConversation.value?.directConversationId == directConversationId) {
      return true
    }

    const directConversation =
      await ConversationService.getDirectConversationById(directConversationId)

    // Another conversation was requested while loading: keep that one
    if (requestedId !== directConversationId) return false

    if (!directConversation) {
      currentConversation.value = undefined
      return false
    }

    const friend =
      directConversation.user1.id == userId ? directConversation.user2 : directConversation.user1
    currentConversation.value = {
      chatConversationId: directConversation.chatConversationId,
      directConversationId: directConversation.id,
      friend: friend,
    }
    return true
  }

  // Selected from the friends list: the data is already there, no request needed
  function selectFriendConversation(conversation: FriendConversation) {
    requestedId = conversation.directConversationId
    currentConversation.value = toFriendConversation(conversation)
  }

  // No conversation open (e.g. /chat/direct without id)
  function clearDirectConversation() {
    requestedId = undefined
    currentConversation.value = undefined
  }

  return {
    currentConversation,
    updateDirectConversation,
    selectFriendConversation,
    clearDirectConversation,
  }
})
