import { ref, type Ref } from 'vue'
import { defineStore } from 'pinia'
import { type ChatServer, type ChatChannel, type ChatMessage, type WebSocketMessage } from '@/types'
import ChatServerService from '@/services/chatServer/ChatServerService'
import ChatChannelService from '@/services/chatChannel/ChatChannelService'
import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService'
import ChatMessageService from '@/services/chatMessage/ChatMessageService'

export const serverInfoStore = defineStore('serverInfo', () => {
  const currentChatChannel = ref<ChatChannel>()
  const channels = ref<ChatChannel[]>([])
  const channelMessages = ref<Record<string, ChatMessage[]>>({})
  const pendingMessages = ref<Record<string, Record<string, ChatMessage>>>({})
  const servers = ref<ChatServer[]>([])
  const currentServer = ref<ChatServer>()

  function setCurrentChatChannel(channel: ChatChannel) {
    currentChatChannel.value = channel
  }

  function addNewChannel(channel: ChatChannel) {
    if (channels.value.includes(channel))
      return false;

    channels.value.push(channel)
    channelMessages.value[channel.id] = [];
    pendingMessages.value[channel.id] = {};
    return true;
  }

  function clearChannels() {
    channels.value.length = 0;
    if (currentChatChannel.value) {
      delete channelMessages.value[currentChatChannel.value!.id];
      delete pendingMessages.value[currentChatChannel.value!.id];
    }
  }

  function newMessage(message: WebSocketMessage) {
    const channel = message.channel as string;
    channelMessages.value[channel]?.unshift(JSON.parse(message.payload) as ChatMessage);
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
    channelMessages.value[channel.id] = messages;
    pendingMessages.value[channel.id] = {};
  }

  async function selectChannel(channel: ChatChannel) {
    wsMessageService.subscribe(channel.id, (message: WebSocketMessage) => {
      newMessage(message)
    });

    const messages = await ChatMessageService.getMessagesByChannelId(channel.id);
    setChannelMessages(channel, messages)
    setCurrentChatChannel(channel);
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
    if (currentServer?.value) {
      await wsMessageService.connect(async () => {
        if (currentServer?.value)
          await fetchServerChannels(currentServer.value);
      });
    }
  }

  async function fetchServerChannels(chatServer: ChatServer): Promise<void> {
    const channels = await ChatChannelService.getChannelsByServerId(chatServer.id);
    await unsubscribeFromCurrentChannel();
    clearChannels();
    setServerChannels(channels);
    currentChatChannel.value = undefined;

    if (channels && channels.length > 0) {
      await selectChannel(channels[0]!)
    }
  }

  return { currentChatChannel, selectChannel, channels, addPendingMessage, confirmMessage, channelMessages, pendingMessages, setCurrentChatChannel, addNewChannel, newMessage, setServerChannels, setChannelMessages, getChatServers, addChatServer, servers, setCurrentServer, currentServer, fetchServerChannels }
})
