<script setup lang="ts">
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import { serverInfoStore } from "@/stores/serverInfo";
import { userInfoStore } from "@/stores/userInfo";
import type { ChatMessage } from "@/types";
import { onMounted, ref } from "vue";

const serverInfo = serverInfoStore();
const userInfo = userInfoStore();
const message = ref("");


async function sendMessage() {
    if (!message.value)
        return;
    const chatMessage = {
        content: message.value
    } as ChatMessage
    message.value = "";

    const ackPromise = await wsMessageService.sendMessage(serverInfo.currentChatChannel!, chatMessage);
    chatMessage.createdAt = new Date().toISOString();
    serverInfo.addPendingMessage(ackPromise.id, chatMessage);

    const wsMessage = await ackPromise.promise;
    serverInfo.confirmMessage(wsMessage)
}


</script>

<template>
    <div id="message-input-bar">
        <div id="message-box">
            <input v-model="message" type="text" id="message-input" @keyup.enter="sendMessage" />
            <div class="btn" type="button" @click="sendMessage" id="wsmessage-btn">
                <span class="material-symbols-sharp">
                    send
                </span>
            </div>
        </div>
    </div>
</template>

<style>
#message-input-bar {
    margin: 1rem;
    margin-top: 0;
}

#message-box {
    background-color: var(--color-background);
    border: 1px solid var(--color-border);
    border-radius: 10px;
    height: 3rem;
    width: 100%;
    display: flex;
    align-items: center;
    max-width: 80%;
    margin: 0 auto;
}

#message-input {
    all: unset;
    flex-grow: 1;
    height: 100%;
    padding-left: 1rem;

}

.material-symbols-sharp {
    color: rgba(255, 255, 255, 1);
}

.btn {
    padding: 12px;
    align-items: center;
    display: flex;
    margin-left: auto;
}

.btn:hover {
    cursor: pointer;
}
</style>