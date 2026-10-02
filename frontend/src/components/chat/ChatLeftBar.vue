<script setup lang="ts">

import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService";
import {serverInfoStore} from "@/stores/serverInfo"
import type { WebSocketMessage } from "@/types";
import { storeToRefs } from "pinia"; 
import { server } from "typescript";
import { ref } from "vue";

const serverInfo = serverInfoStore();
const { channels, currentChatName } = storeToRefs(serverInfo) as any

const newChannelName = ref("")

function addNewChannel() {
    if (newChannelName.value) {
        if (!serverInfo.addNewChannel(newChannelName.value)) {
            return;
        }
        wsMessageService.subscribe(newChannelName.value, (message: WebSocketMessage) =>{
             serverInfo.newMessage(message)
        });
        
        serverInfo.setCurrentChatName(newChannelName.value);
        newChannelName.value = "";
    }
}

function selectChannel(channel: string) {
    serverInfo.setCurrentChatName(channel);
}

</script>

<template>
    <div id="left-bar">
        <div> Add New Channel</div>
        <div style="margin: .2rem 0">
            <input type="text" @keyup.enter="addNewChannel" v-model="newChannelName"/>
        </div>
        <div>
            <button @click="addNewChannel" > Add channel</button>
        </div>
        <div style="margin: 1rem 0;">
            Channel list
        </div>
        <div v-for="channel in channels" class="channel-name" :class="{ active: channel == currentChatName}" @click="selectChannel(channel)">
            {{ channel }} 
        </div>
    </div>

</template>

<style> 
    #left-bar{
        padding: 1rem 0;
    }

    #left-bar > .channel-active {
        background-color: var(--color-background-soft);
    }

    #left-bar > * {
        padding: 0 1rem;
    }

    .channel-name{
        padding: 0 2rem;
    }

    .channel-name:hover{
        cursor:pointer;
        background-color: var(--color-background-light);
    }

    .channel-name.active {
        background-color: var(--color-background-light);
    }
</style>