import { ref, type Ref } from 'vue'
import { defineStore } from 'pinia'
import type { ChatMessage, WebSocketMessage } from '@/types'

export const serverInfoStore = defineStore('serverInfo', () => {
  const currentChatName = ref("")
  const channels = ref([] as string[])
  const channelMessages: Ref<Record<string, ChatMessage[]>> = ref({})
  const pendingMessages: Ref<Record<string, Record<string, ChatMessage>>> = ref({})

  function setCurrentChatName(name: string) {
    currentChatName.value = name
  }

  function addNewChannel(channel: string) {
    if (channels.value.includes(channel))
      return false;

    channels.value.push(channel)
    channelMessages.value[channel] = [];
    pendingMessages.value[channel] = {};
    return true;
  }

  function newMessage(message: WebSocketMessage) {
    const channel = message.channel as string;
    channelMessages.value[channel]?.unshift(JSON.parse(message.payload) as ChatMessage);
  }

  function addPendingMessage(id: string, chatMessage: ChatMessage) {
    const channel = currentChatName.value;
    pendingMessages.value[channel]![id] = chatMessage
  }

  function confirmMessage(message: WebSocketMessage) {
    const channel = currentChatName.value
    const chatMessage = pendingMessages.value[channel]![message.id!];
    if (chatMessage) {
      delete pendingMessages.value[channel]![message.id!];
      newMessage(message);
    }
  }

  return { currentChatName, channels, addPendingMessage, confirmMessage, channelMessages, pendingMessages, setCurrentChatName, addNewChannel, newMessage }
})
