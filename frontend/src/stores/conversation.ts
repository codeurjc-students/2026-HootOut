import { defineStore } from 'pinia'

import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
import ChatMessageService from '@/services/chatMessage/ChatMessageService'
import type { ChatMessage, PageResult, WebSocketMessage } from '@/types'
import { ref } from 'vue'

export const conversationStore = defineStore('conversation', () => {
  const currentConversationId = ref<string>('')
  const currentMessages = ref<ChatMessage[]>([])
  const pendingMessages = ref<Record<string, Record<string, ChatMessage>>>({})
  const lastPage = ref<PageResult<ChatMessage>>()

  // Incremented every time the open conversation changes (or is about to change). A request
  // that started with an older value finished too late and its result is ignored.
  // It is not returned by the store: Pinia would expose a copy of the number, so other
  // stores must call cancelLoading() instead of changing it.
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

  function addMessage(message: WebSocketMessage) {
    const chatMessage = parseChatMessage(message.payload)
    if (!chatMessage) return
    currentMessages.value.unshift(chatMessage)
  }

  function newMessage(message: WebSocketMessage) {
    // Messages of another conversation (e.g. if unsubscribing from the previous one failed)
    if (message.channel && message.channel !== currentConversationId.value) return
    addMessage(message)
  }

  function confirmMessage(message: WebSocketMessage) {
    const conversationId = currentConversationId?.value
    const pending = conversationId ? pendingMessages.value[conversationId] : undefined
    // Not pending in the open conversation (e.g. the ack arrived after changing conversation):
    // nothing to do, the message is loaded with the history when that conversation is opened
    if (!pending || !message.replyTo || !pending[message.replyTo]) return

    delete pending[message.replyTo]
    // Already checked that it belongs to the open conversation
    addMessage(message)
  }

  async function unsubscribeFromConversation(conversationId: string | undefined) {
    if (conversationId) {
      await wsMessageService.unsubscribe(conversationId)
    }
  }

  // Makes any request that is still loading for the open conversation be ignored
  function cancelLoading() {
    selection++
  }

  async function selectConversation(conversationId: string) {
    clearConversation()
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
    // Another conversation (or server) was selected while loading: keep that one
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
    // The conversation changed while loading: these messages belong to the previous one
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
    currentMessages.value = []
    lastPage.value = { items: [], next: null, pageSize: 10, total: -1 }
    if (currentConversationId?.value) {
      delete pendingMessages.value[currentConversationId.value]
    }
  }

  // Leaves the open conversation (e.g. when changing server): stops loading it,
  // clears its messages and unsubscribes from it
  function closeConversation() {
    const conversationId = currentConversationId.value
    cancelLoading()
    clearConversation()
    currentConversationId.value = ''
    if (!conversationId) return
    try {
      wsMessageService.unsubscribe(conversationId)
    } catch {
      // The socket is not connected: there is no subscription to remove
    }
  }

  return {
    lastPage,
    currentMessages,
    pendingMessages,
    currentConversationId,
    addPendingMessage,
    confirmMessage,
    newMessage,
    setCurrentMessages,
    getNextMessages,
    clearConversation,
    closeConversation,
    cancelLoading,
    selectConversation,
  }
})
