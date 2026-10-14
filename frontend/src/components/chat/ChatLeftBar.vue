<script setup lang="ts">

import ChatChannelService from "@/services/chatChannel/ChatChannelService";
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService";
import { serverInfoStore } from "@/stores/serverInfo"
import type { ChatChannel, WebSocketMessage } from "@/types";
import { storeToRefs } from "pinia";
import { ref } from "vue";

const serverInfo = serverInfoStore();
const { channels, currentChatChannel, currentServer } = storeToRefs(serverInfo) as any
const newChannelName = ref("")


async function addNewChannel() {
    if (newChannelName.value) {

        const newChannel = await ChatChannelService.createNewChannel({ name: newChannelName.value, chatServerId: serverInfo.currentServer?.id ?? "" });

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

async function selectChannel(channel: ChatChannel) {
    serverInfo.selectChannel(channel);
}

</script>

<template>
    <div id="left-bar">
        <div v-if="currentServer">
            <div> Server: </div>
            <div> {{ currentServer?.name }}</div>

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
            <div v-for="channel in channels" class="channel-name pointer"
                :class="{ 'background-light': channel.id == currentChatChannel?.id }" :key="channel.id"
                @click="selectChannel(channel)">
                {{ channel.name }}
            </div>
        </div>
        <div v-else>
            Select a Server First
        </div>
    </div>

</template>

<style>
#left-bar {
    padding: 1rem 0;
}

#left-bar>* {
    padding: 0 1rem;
}

.channel-name {
    padding: 0 2rem;
}

.channel-name:hover {
    background-color: rgb(var(--color-background-light));
}
</style>