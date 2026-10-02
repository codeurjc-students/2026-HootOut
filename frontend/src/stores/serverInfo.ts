import { ref } from 'vue'
import { defineStore } from 'pinia' 
import type { ChatMessage, WebSocketMessage } from '@/types'

export const serverInfoStore = defineStore('serverInfo', () => {
  const currentChatName = ref("")
  const channels = ref([] as string[])
  const channelMessages = ref({} as Record<string, ChatMessage[]>)

  function setCurrentChatName(name: string) {
    currentChatName.value = name
  }

  function addNewChannel(channel: string) {
    if (channels.value.includes(channel)) 
      return false;

    channels.value.push(channel)
    channelMessages.value[channel] = [];
    return true;
  }

  function newMessage(message: WebSocketMessage) {
    const channel = message.channel as string;
    channelMessages.value[channel]?.unshift(JSON.parse(message.payload) as ChatMessage);
  } 

  return { currentChatName, channels, channelMessages, setCurrentChatName, addNewChannel, newMessage }
})
