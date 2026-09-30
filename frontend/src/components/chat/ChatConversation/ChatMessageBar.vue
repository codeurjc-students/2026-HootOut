<script setup lang="ts">
import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import { onMounted, ref } from "vue";


const message = ref("");

onMounted(() => {

});

function sendMessage() {
    wsMessageService.sendMessage("chat123", message.value);
}


</script>

<template>
    <div id="message-input-bar">
        <div id="message-box">
            <div>
            <p>Send Message:</p>
            <input v-model="message" type="text" id="wsmessage-input" />
            <button type="button" @click="sendMessage" id="wsmessage-btn">Send Message</button>
        </div>
        </div>
    </div>
</template>

<style>
#message-input-bar {
    margin: 1rem;
}

#message-box {
    background-color: var(--color-background);
    border: 1px solid var(--color-border);
    border-radius: 10px;
    height: 3rem;
    width: 100%;
}
</style>