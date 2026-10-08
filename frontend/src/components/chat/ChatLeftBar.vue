<script setup lang="ts">

import ChatChannelService from "@/services/chatChannel/ChatChannelService";
import ChatMessageService from "@/services/chatMessage/ChatMessageService";
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService";
import { serverInfoStore } from "@/stores/serverInfo"
import type { ChatChannelDto, WebSocketMessage } from "@/types";
import { storeToRefs } from "pinia";
import { onMounted, ref } from "vue";

const serverInfo = serverInfoStore();
const { channels, currentChatChannel } = storeToRefs(serverInfo) as any
const newChannelName = ref("")

onMounted(async () => {
    wsMessageService.connect(async () => {
        const channels = await ChatChannelService.getAllChannels();
        serverInfo.setServerChannels(channels)
        channels.forEach(channel => {
            wsMessageService.connect(() => { });
            wsMessageService.subscribe(channel.id, (message: WebSocketMessage) => {
                serverInfo.newMessage(message)
            });
        })
    });
});

async function addNewChannel() {
    if (newChannelName.value) {

        const newChannel = await ChatChannelService.createNewChannel(newChannelName.value)

        if (!serverInfo.addNewChannel(newChannel)) {
            return;
        }

        wsMessageService.subscribe(newChannel.id, (message: WebSocketMessage) => {
            serverInfo.newMessage(message)
        });

        serverInfo.setCurrentChatChannel(newChannel);
        newChannelName.value = "";
    }
}

async function selectChannel(channel: ChatChannelDto) {
    const messages = await ChatMessageService.getMessagesByChannelId(channel.id);
    serverInfo.setChannelMessages(channel, messages)
    serverInfo.setCurrentChatChannel(channel);
}

</script>

<template>
    <div id="left-bar">
        <div> Add New Channel</div>
        <div style="margin: .2rem 0">
            <input type="text" @keyup.enter="addNewChannel" v-model="newChannelName" />
        </div>
        <div>
            <button @click="addNewChannel"> Add channel</button>
        </div>
        <div style="margin: 1rem 0;">
            Channel list
        </div>
        <div v-for="channel in channels" class="channel-name" :class="{ active: channel.id == currentChatChannel?.id }"
            @click="selectChannel(channel)">
            {{ channel.name }}
        </div>
    </div>

</template>

<style>
#left-bar {
    padding: 1rem 0;
}

#left-bar>.channel-active {
    background-color: var(--color-background-soft);
}

#left-bar>* {
    padding: 0 1rem;
}

.channel-name {
    padding: 0 2rem;
}

.channel-name:hover {
    cursor: pointer;
    background-color: var(--color-background-light);
}

.channel-name.active {
    background-color: var(--color-background-light);
}
</style>