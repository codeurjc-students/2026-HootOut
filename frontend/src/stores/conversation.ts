import { defineStore } from 'pinia'

import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
import ChatMessageService from '@/services/chatMessage/ChatMessageService'
import type { ChatMessage, PageResult, WebSocketMessage } from '@/types'
import { ref } from 'vue'

export const conversationStore = defineStore('conversation', () => {
  const currentConversationId = ref<string>()
  const currentMessages = ref<ChatMessage[]>([])
  const pendingMessages = ref<Record<string, Record<string, ChatMessage>>>({})
  const lastPage = ref<PageResult<ChatMessage>>()

  // Incremented every time the open channel changes (or is about to change). A request
  // that started with an older value finished too late and its result is ignored.
  let selection = 0

  // Returns null when the payload is missing or is not valid JSON
  function parseChatMessage(payload: string | undefined): ChatMessage | null {
    if (!payload) return null
    try {
      return JSON.parse(payload) as ChatMessage
    } catch {
      return null
    }
  }

  function newMessage(message: WebSocketMessage) {
    const chatMessage = parseChatMessage(message.payload)
    if (!chatMessage) return
    currentMessages.value.unshift(chatMessage)
  }

  function confirmMessage(message: WebSocketMessage) {
    const conversationId = currentConversationId?.value
    const pending = conversationId ? pendingMessages.value[conversationId] : undefined
    // Not pending in the open channel (e.g. the ack arrived after changing channel):
    // nothing to do, the message is loaded with the history when that channel is opened
    if (!pending || !message.replyTo || !pending[message.replyTo]) return

    delete pending[message.replyTo]
    newMessage(message)
  }

  async function unsubscribeFromConversation(conversationId: string | undefined) {
    if (conversationId) {
      await wsMessageService.unsubscribe(conversationId)
    }
  }

  async function selectConversation(conversationId: string) {
    const currentSelection = ++selection
    const oldConversation = currentConversationId.value
    currentConversationId.value = conversationId

    try {
      await unsubscribeFromConversation(oldConversation)
      wsMessageService.subscribe(conversationId, (message: WebSocketMessage) => {
        newMessage(message)
      })
    } catch (error) {
      wsMessageService.addPendingSubscription(conversationId, (message: WebSocketMessage) => {
        newMessage(message)
      })
    }

    const pageResult = await ChatMessageService.getMessagesByConversationId(conversationId)
    // Another channel (or server) was selected while loading: keep that one
    if (currentSelection !== selection) return

    lastPage.value = pageResult
    setCurrentMessages(conversationId, pageResult.items)
  }

  async function setCurrentMessages(conversationId: string, messages: ChatMessage[]) {
    currentMessages.value = messages
    pendingMessages.value[conversationId] = {}
  }

  async function getNextMessages() {
    const conversationId = currentConversationId?.value
    // No more pages: requesting with next = null would load the first page again
    if (!conversationId || !lastPage.value?.next) return

    const currentSelection = selection
    const pageResult = await ChatMessageService.getMessagesByConversationId(
      conversationId,
      lastPage.value.next,
      lastPage.value.pageSize
    )
    // The channel changed while loading: these messages belong to the previous one
    if (currentSelection !== selection) return

    lastPage.value = pageResult
    if (pageResult.items.length > 0) {
      currentMessages.value.push(...pageResult.items)
    }
  }

  function addPendingMessage(id: string, chatMessage: ChatMessage) {
    const conversationId = currentConversationId?.value
    if (!conversationId) return
    ;(pendingMessages.value[conversationId] ??= {})[id] = chatMessage
  }

  function clearConversation() {
    if (currentConversationId?.value) {
      currentMessages.value = []
      delete pendingMessages.value[currentConversationId.value]
    }
  }

  return {
    lastPage,
    currentMessages,
    pendingMessages,
    selection,
    currentConversationId,
    addPendingMessage,
    confirmMessage,
    newMessage,
    setCurrentMessages,
    getNextMessages,
    clearConversation,
    selectConversation,
  }
})
