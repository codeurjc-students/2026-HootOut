import { ref } from 'vue'
import { defineStore } from 'pinia'
import { type ChatServer, type ChatChannel, type PageResult } from '@/types'
import ChatServerService from '@/services/chatServer/ChatServerService'
import ChatChannelService from '@/services/chatChannel/ChatChannelService'
import { conversationStore } from './conversation'

export const serverInfoStore = defineStore('serverInfo', () => {
  const currentChatChannel = ref<ChatChannel>()
  const channels = ref<ChatChannel[]>([])
  const servers = ref<ChatServer[]>([])
  const currentServer = ref<ChatServer>()

  const conversation = conversationStore()

  function setCurrentChatChannel(channel: ChatChannel | undefined) {
    currentChatChannel.value = channel
  }

  function addNewChannel(channel: ChatChannel) {
    if (channels.value.includes(channel)) return false
    channels.value.push(channel)
    return true
  }

  function clearChannels() {
    channels.value.length = 0
    // The open conversation belongs to a channel of this list: leave it
    conversation.closeConversation()
  }

  function setServerChannels(newChannels: ChatChannel[]) {
    newChannels.forEach((c) => addNewChannel(c))
  }

  async function selectChannel(channelId: string) {
    const channel = channels.value.filter((x) => x.id == channelId).at(0)
    if (!channel) return

    setCurrentChatChannel(channel)
    // Waits for the messages, so errors reach the caller
    await conversation.selectConversation(channel.chatConversationId)
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
    // A conversation of the previous server that is still loading must not be opened
    conversation.cancelLoading()
    if (makeRequest)
      currentServer.value!.channels = await ChatChannelService.getChannelsByServerId(chatServer.id)
    const channels = currentServer.value?.channels ?? []
    clearChannels()
    setServerChannels(channels)
    setCurrentChatChannel(undefined)
  }

  function updateChannel(updated: ChatChannel) {
    const channel = channels.value.find((x) => x.id == updated.id)
    if (channel) channel.name = updated.name
    if (currentChatChannel.value?.id == updated.id) currentChatChannel.value.name = updated.name
  }

  return {
    currentChatChannel,
    servers,
    channels,
    currentServer,
    setCurrentChatChannel,
    addNewChannel,
    selectChannel,
    setServerChannels,
    getChatServers,
    addChatServer,
    setCurrentServer,
    fetchServerChannels,
    setCurrentServerSettings,
    updateChannel,
  }
})
