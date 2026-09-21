<script setup>
import { onMounted, onUnmounted, ref } from 'vue';
import webSocketService from '@/services/common/websockets/WebSocketService';

const wsStatus = ref('');
const wsMessage = ref('');
const message = ref('');

onMounted(() => {
    webSocketService.connect(import.meta.env.VITE_WSS_URL, {
        onOpen: () => { wsStatus.value = 'Connection Opened'; },
        onClose: () => { wsStatus.value = 'Connection Closed'; },
        onError: () => { wsStatus.value = 'Connection Error'; },
        onMessage: (event) => { wsMessage.value = event.data; },
    });
});

onUnmounted(() => {
    webSocketService.close();
});

function sendMessage() {
    webSocketService.send(message.value);
}
</script>

<template>
    <section id="websockets">
        <h2>Websockets:</h2>
        <div id="wsstatus">{{ wsStatus }}</div>
        <div>
            <p>Send Message:</p>
            <input v-model="message" type="text" id="wsmessage-input" />
            <button type="button" @click="sendMessage" id="wsmessage-btn">Send Message</button>
        </div>
        <div>
            <p>Received message:</p>
            <div id="wsmessage">{{ wsMessage }}</div>
        </div>
    </section>
</template>

<style>
#websockets {
    margin-top: 2rem;
    flex-grow: 1;
}
</style>