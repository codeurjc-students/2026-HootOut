<script setup lang="ts">
import FilePreview from "@/components/shared/filePreview/FilePreview.vue";
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import MediaFileService from "@/services/mediaFile/MediaFileService";
import { serverInfoStore } from "@/stores/serverInfo";
import type { ChatMessage, MessageAttachment, UploadFileResponse } from "@/types";
import { storeToRefs } from "pinia";
import { ref } from "vue";

const serverInfo = serverInfoStore();
const { currentChatChannel } = storeToRefs(serverInfo);

const message = ref("");
const fileInput = ref<HTMLInputElement>();
const imgTemp = ref<HTMLImageElement>();
const attachments = ref<MessageAttachment[]>([])

async function sendMessage() {
    if (!message.value)
        return;
    const chatMessage: ChatMessage = {
        content: message.value,
        attachments: attachments.value
    } as ChatMessage
    message.value = "";

    const ackPromise = await wsMessageService.sendMessage(serverInfo.currentChatChannel!, chatMessage);
    chatMessage.createdAt = new Date().toISOString();
    serverInfo.addPendingMessage(ackPromise.id, chatMessage);

    const wsMessage = await ackPromise.promise;
    serverInfo.confirmMessage(wsMessage)

    attachments.value.length = 0;
}

async function selectFile() {
    fileInput?.value?.click();
}

async function uploadFile() {
    const files = fileInput.value?.files;
    if (!files || files.length == 0) {
        return;
    }

    const response = await MediaFileService.uploadFile(files.item(0))
    if (response)
        attachments.value?.push({ mediaFileId: response.id, contentType: response.contentType, url: response.url });
}

function removeFile(file: MessageAttachment) {
    attachments.value = attachments.value?.filter(x => x.id != file.id);
}

</script>

<template>
    <div class="file-preview-list p-1">
        <div v-for="attachment in attachments" class="file-preview-entry background-soft pointer" :key="attachment.id">
            <FilePreview :file="attachment" />
            <div class="material-symbols-sharp pointer inline-btn file-preview-close-btn"
                @click="removeFile(attachment)">close
            </div>
        </div>
    </div>
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

.file-preview-list {
    display: flex;
    width: 100%;
    overflow: auto;
    gap: 1rem;
}

.file-preview-entry {
    display: flex;
    justify-content: center;
    border: 1px solid rgb(var(--color-border));
    border-radius: .5rem;
    padding: 1rem;
    position: relative;
}

.file-preview-entry:hover::before {
    content: '\A';
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(var(--color-background-light), 0.2);
    pointer-events: none;
}

.file-preview-close-btn {
    position: absolute;
    right: 0;
    top: 0;
    color: rgb(var(--color-warning-light))
}
</style>