<script setup lang="ts">
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import MediaFileService from "@/services/mediaFile/MediaFileService";
import { serverInfoStore } from "@/stores/serverInfo";
import type { ChatMessage, UploadFileResponse } from "@/types";
import { storeToRefs } from "pinia";
import { computed, ref } from "vue";

const serverInfo = serverInfoStore();
const { currentChatChannel } = storeToRefs(serverInfo);

const message = ref("");
const fileInput = ref<HTMLInputElement>();
const imgTemp = ref<HTMLImageElement>();
const uploadedImageTemp = ref<UploadFileResponse>();

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

async function selectFile() {
    fileInput?.value?.click();
}

async function uploadFile() {
    const files = fileInput.value?.files;
    if (!files || files.length == 0) {
        return;
    }

    uploadedImageTemp.value = await MediaFileService.uploadFile(files.item(0));
    await showImage();
}

async function showImage() {
    if (!uploadedImageTemp?.value) return;
    const blob = await MediaFileService.getBlob(uploadedImageTemp.value?.id);
    const reader = new FileReader();

    await new Promise((resolve, reject) => {
        reader.onload = resolve;
        reader.onerror = reject;
        reader.readAsDataURL(blob);
    });

    imgTemp.value!.src = reader.result?.toString() ?? "";
    return imgTemp.value?.src;
}

</script>

<template>
    <img v-if="uploadedImageTemp" ref="imgTemp" />
    <div id="message-input-bar">
        <div id="message-box" class="background-primary">
            <div id="message-box-icons">
                <div class="inline-btn material-symbols-sharp pointer" @click="selectFile">
                    add
                </div>
                <input id="file-input" ref="fileInput" type="file" hidden @change="uploadFile" />
            </div>
            <input v-model="message" type="text" id="message-input" @keyup.enter="sendMessage"
                :placeholder="`Write a message to ${currentChatChannel?.name}`" />
            <div class="inline-btn pointer material-symbols-sharp" type="button" @click="sendMessage"
                id="wsmessage-btn">
                send
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
    border: 1px solid rgb(var(--color-border));
    border-radius: 10px;
    height: 3rem;
    width: 100%;
    display: flex;
    align-items: center;
    max-width: 80%;
    margin: 0 auto;
    padding: .75rem;
}

#message-input {
    all: unset;
    flex-grow: 1;
    height: 100%;
    padding-left: 1rem;
}

#message-box-icons {
    align-items: center;
    display: flex;
}
</style>