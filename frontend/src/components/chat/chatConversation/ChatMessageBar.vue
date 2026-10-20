<script setup lang="ts">
import FilePreviewList from "@/components/shared/filePreview/FilePreviewList.vue";
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import MediaFileService from "@/services/mediaFile/MediaFileService";
import { serverInfoStore } from "@/stores/serverInfo";
import type { ChatMessage, MessageAttachment } from "@/types";
import { storeToRefs } from "pinia";
import { ref } from "vue";

const serverInfo = serverInfoStore();
const { currentChatChannel } = storeToRefs(serverInfo);

const message = ref("");
const fileInput = ref<HTMLInputElement>();
const attachments = ref<MessageAttachment[]>([])

async function sendMessage() {
    if (!message.value && attachments.value.length == 0)
        return;
    const chatMessage: ChatMessage = {
        content: message.value,
        attachments: attachments.value.flatMap(x => <MessageAttachment>{ mediaFileId: x.mediaFileId })
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
        attachments.value?.push({ mediaFileId: response.id, contentType: response.contentType, url: response.url, fileName: response.fileName });
}

function removeFile(file: MessageAttachment) {
    attachments.value = attachments.value?.filter(x => x.mediaFileId != file.mediaFileId);
}

</script>

<template>
    <FilePreviewList v-if="attachments.length > 0" :files="attachments" :canRemove="true" @remove="removeFile" />
    <div id="message-input-bar">
        <div id="message-box" class="background-primary">
            <div id="message-box-icons">
                <div class="inline-btn material-symbols-sharp pointer" @click="selectFile">
                    add
                </div>
                <input id="file-input" ref="fileInput" type="file" hidden @change="uploadFile" />
            </div>
            <input v-model="message" type="text" id="message-input" @keyup.enter="sendMessage"
                :placeholder="`Write a message to ${currentChatChannel?.name}`" autocomplete="off" />
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

.file-preview-close-btn {
    position: absolute;
    right: 0;
    top: 0;
    color: rgb(var(--color-warning-light))
}
</style>