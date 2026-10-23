import { ref, type Ref } from 'vue'
import { defineStore } from 'pinia'
import { type ChatServer, type ChatChannel, type ChatMessage, type WebSocketMessage, type PageResult } from '@/types'
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

  function addNewChannel(channel: ChatChannel) {
    if (channels.value.includes(channel))
      return false;

    channels.value.push(channel)
    pendingMessages.value[channel.id] = {};
    return true;
  }

  function clearChannels() {
    channels.value.length = 0;
    if (currentChatChannel.value) {
      channelMessages.value = [];
      delete pendingMessages.value[currentChatChannel.value!.id];
    }
  }

  function newMessage(message: WebSocketMessage) {
    channelMessages.value?.unshift(JSON.parse(message.payload) as ChatMessage);
  }

  function addPendingMessage(id: string, chatMessage: ChatMessage) {
    const channel = currentChatChannel.value;
    pendingMessages.value[channel!.id]![id] = chatMessage
  }

  function confirmMessage(message: WebSocketMessage) {
    const channel = currentChatChannel.value
    const chatMessage = pendingMessages.value[channel!.id]![message.replyTo!];
    if (chatMessage) {
      delete pendingMessages.value[channel!.id]![message.replyTo!];
      newMessage(message);
    }
  }

  function setServerChannels(newChannels: ChatChannel[]) {
    newChannels.forEach(c => addNewChannel(c));
  }

  async function unsubscribeFromCurrentChannel() {
    const currentChannel = currentChatChannel.value;
    if (currentChannel) {
      await wsMessageService.unsubscribe(currentChannel.id, (message: WebSocketMessage) => {
        newMessage(message)
      });
    }
  }

  function setChannelMessages(channel: ChatChannel, messages: ChatMessage[]) {
    channelMessages.value = messages;
    pendingMessages.value[channel.id] = {};
  }

  async function selectChannel(channelId: string) {
    const channel = channels.value.filter(x => x.id == channelId).at(0);
    if (!channel)
      return;

    try {
      wsMessageService.subscribe(channelId, (message: WebSocketMessage) => {
        newMessage(message)
      });
    } catch (error) {
      wsMessageService.addPendingSubscription(channelId, (message: WebSocketMessage) => {
        newMessage(message)
      });
    }

    const pageResult = await ChatMessageService.getMessagesByChannelId(channelId);
    lastPage.value = pageResult;
    setChannelMessages(channel, pageResult.items)
    currentChatChannel.value = channel
  }

  async function getNextMessages() {
    if (lastPage.value) {
      const pageResult = await ChatMessageService.getMessagesByChannelId(currentChatChannel.value!.id, lastPage.value.next, lastPage.value.pageSize);
      lastPage.value = pageResult;
      if (pageResult.items.length > 0) {
        channelMessages.value.push(...pageResult.items);
      }
    }
  }

  async function getChatServers(): Promise<Ref<ChatServer[]>> {
    servers.value = await ChatServerService.getServers();
    return servers;
  }

  function addChatServer(chatServer: ChatServer): void {
    servers.value.push(chatServer);
  }

  async function setCurrentServer(id: string): Promise<void> {
    currentServer.value = servers.value.filter(x => x.id == id).at(0);
    if (currentServer?.value)
      await fetchServerChannels(currentServer.value);
  }

  async function fetchServerChannels(chatServer: ChatServer): Promise<void> {
    const channels = await ChatChannelService.getChannelsByServerId(chatServer.id);
    clearChannels();
    setServerChannels(channels);
    currentChatChannel.value = undefined;
  }

  return { currentChatChannel, selectChannel, channels, lastPage, addPendingMessage, confirmMessage, channelMessages, pendingMessages, newMessage, setServerChannels, setChannelMessages, getChatServers, addChatServer, servers, setCurrentServer, currentServer, fetchServerChannels, getNextMessages }
})
