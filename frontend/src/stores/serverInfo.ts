import { ref, type Ref } from 'vue'
import { defineStore } from 'pinia'
import {
  type ChatServer,
  type ChatChannel,
  type ChatMessage,
  type WebSocketMessage,
  type PageResult,
} from '@/types'
import ChatServerService from '@/services/chatServer/ChatServerService'
import ChatChannelService from '@/services/chatChannel/ChatChannelService'
import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
import ChatMessageService from '@/services/chatMessage/ChatMessageService'

export const serverInfoStore = defineStore('serverInfo', () => {
  const currentChatChannel = ref<ChatChannel>()
  const channels = ref<ChatChannel[]>([])
  const channelMessages = ref<ChatMessage[]>([])
  const pendingMessages = ref<Record<string, Record<string, ChatMessage>>>({})
  const servers = ref<ChatServer[]>([])
  const currentServer = ref<ChatServer>()
  const lastPage = ref<PageResult<ChatMessage>>()

  // Incremented every time the open channel changes (or is about to change). A request
  // that started with an older value finished too late and its result is ignored.
  let selection = 0

  function setCurrentChatChannel(channel: ChatChannel | undefined) {
    currentChatChannel.value = channel
  }

  function addNewChannel(channel: ChatChannel) {
    if (channels.value.includes(channel)) return false

    channels.value.push(channel)
    pendingMessages.value[channel.id] = {}
    return true
  }

  function clearChannels() {
    channels.value.length = 0
    if (currentChatChannel.value) {
      channelMessages.value = []
      delete pendingMessages.value[currentChatChannel.value!.id]
    }
  }

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
    channelMessages.value.unshift(chatMessage)
  }

  function addPendingMessage(id: string, chatMessage: ChatMessage) {
    const channel = currentChatChannel.value
    if (!channel) return
    ;(pendingMessages.value[channel.id] ??= {})[id] = chatMessage
  }

  function confirmMessage(message: WebSocketMessage) {
    const channel = currentChatChannel.value
    const pending = channel ? pendingMessages.value[channel.id] : undefined
    // Not pending in the open channel (e.g. the ack arrived after changing channel):
    // nothing to do, the message is loaded with the history when that channel is opened
    if (!pending || !message.replyTo || !pending[message.replyTo]) return

    delete pending[message.replyTo]
    newMessage(message)
  }

  function setServerChannels(newChannels: ChatChannel[]) {
    newChannels.forEach((c) => addNewChannel(c))
  }

  async function unsubscribeFromCurrentChannel() {
    const currentChannel = currentChatChannel.value
    if (currentChannel) {
      await wsMessageService.unsubscribe(currentChannel.id, (message: WebSocketMessage) => {
        newMessage(message)
      })
    }
  }

  function setChannelMessages(channel: ChatChannel, messages: ChatMessage[]) {
    channelMessages.value = messages
    pendingMessages.value[channel.id] = {}
  }

  async function selectChannel(channelId: string) {
    const channel = channels.value.filter((x) => x.id == channelId).at(0)
    if (!channel) return

    const currentSelection = ++selection

    try {
      wsMessageService.subscribe(channelId, (message: WebSocketMessage) => {
        newMessage(message)
      })
    } catch (error) {
      wsMessageService.addPendingSubscription(channelId, (message: WebSocketMessage) => {
        newMessage(message)
      })
    }

    const pageResult = await ChatMessageService.getMessagesByChannelId(channelId)
    // Another channel (or server) was selected while loading: keep that one
    if (currentSelection !== selection) return

    lastPage.value = pageResult
    setChannelMessages(channel, pageResult.items)
    setCurrentChatChannel(channel)
  }

  async function getNextMessages() {
    const channel = currentChatChannel.value
    // No more pages: requesting with next = null would load the first page again
    if (!channel || !lastPage.value?.next) return

    const currentSelection = selection
    const pageResult = await ChatMessageService.getMessagesByChannelId(
      channel.id,
      lastPage.value.next,
      lastPage.value.pageSize
    )
    // The channel changed while loading: these messages belong to the previous one
    if (currentSelection !== selection) return

    lastPage.value = pageResult
    if (pageResult.items.length > 0) {
      channelMessages.value.push(...pageResult.items)
    }
  }

  async function getChatServers(next: string | null): Promise<PageResult<ChatServer>> {
    const page = await ChatServerService.getServers(next)
    if (page.items) {
      servers.value.push(...page.items)
    }
    return page
  }

  function addChatServer(chatServer: ChatServer): void {
    servers.value.push(chatServer)
  }

  async function setCurrentServer(id: string): Promise<void> {
    currentServer.value = await ChatServerService.getServerById(id)
    if (currentServer?.value) await fetchServerChannels(currentServer.value)
  }

  async function setCurrentServerSettings(server: ChatServer) {
    currentServer.value = server
  }

  async function fetchServerChannels(
    chatServer: ChatServer,
    makeRequest: boolean = false
  ): Promise<void> {
    // A channel of the previous server that is still loading must not be opened
    selection++
    if (makeRequest)
      currentServer.value!.channels = await ChatChannelService.getChannelsByServerId(chatServer.id)
    const channels = currentServer.value?.channels ?? []
    clearChannels()
    setServerChannels(channels)
    setCurrentChatChannel(undefined)
  }

  return {
    currentChatChannel,
    setCurrentChatChannel,
    addNewChannel,
    selectChannel,
    channels,
    lastPage,
    addPendingMessage,
    confirmMessage,
    channelMessages,
    pendingMessages,
    newMessage,
    setServerChannels,
    setChannelMessages,
    getChatServers,
    addChatServer,
    servers,
    setCurrentServer,
    currentServer,
    fetchServerChannels,
    getNextMessages,
    setCurrentServerSettings,
  }
})
