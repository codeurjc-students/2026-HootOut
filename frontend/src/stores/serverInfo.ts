import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { ChatChannelDto, ChatMessage, WebSocketMessage } from '@/types'

export const serverInfoStore = defineStore('serverInfo', () => {
  const currentChatChannel = ref<ChatChannelDto>()
  const channels = ref<ChatChannelDto[]>([])
  const channelMessages = ref<Record<string, ChatMessage[]>>({})
  const pendingMessages = ref<Record<string, Record<string, ChatMessage>>>({})

  function setCurrentChatChannel(channel: ChatChannelDto) {
    currentChatChannel.value = channel
  }

  function addNewChannel(channel: ChatChannelDto) {
    if (channels.value.includes(channel))
      return false;

    channels.value.push(channel)
    channelMessages.value[channel.id] = [];
    pendingMessages.value[channel.id] = {};
    return true;
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
    const chatMessage = pendingMessages.value[channel!.id]![message.id!];
    if (chatMessage) {
      delete pendingMessages.value[channel!.id]![message.id!];
      newMessage(message);
    }
  }

  function setServerChannels(newChannels: ChatChannelDto[]) {
    newChannels.forEach(c => addNewChannel(c));
  }

  function setChannelMessages(channel: ChatChannelDto, messages: ChatMessage[]) {
    channelMessages.value[channel.id] = messages;
    pendingMessages.value[channel.id] = {};
  }

  return { currentChatChannel, channels, addPendingMessage, confirmMessage, channelMessages, pendingMessages, setCurrentChatChannel, addNewChannel, newMessage, setServerChannels, setChannelMessages }
})
