<script setup>
import { onMounted, ref } from 'vue';

const wsStatus = ref('');
const wsMessage = ref('');
const message = ref('');

onMounted(async () => {
    websocketsService.connect();
});

const websocketsService = {
    socket: WebSocket,
    connect: function () {
        this.socket = new WebSocket(`${import.meta.env.VITE_WSS_URL}`);
        this.socket.onopen = (event) => {
            wsStatus.value = 'Connection Opened';
        }
        this.socket.onclose = (event) => {
            wsStatus.value = 'Connection Closed';
        }
        this.socket.onerror = (event) => {
            wsStatus.value = 'Connection Error';
        }
        this.socket.onmessage = (event) => {
            wsMessage.value = event.data;
        }
    },
    sendMessage: function (message) {
        const data = message.value;
        this.socket.send(data);
    }
}

function sendMessage() {
    websocketsService.sendMessage(message);
}

</script>

<template>
    <section id='websockets'>
        <h2>Websockets:</h2>
        <div> {{ wsStatus }}</div>
        <div>
            <p>Send Message:</p>
            <input v-model="message" type="text" />
            <button type="button" @click="sendMessage">Send Message</button>
        </div>
        <div>
            <p>Received message:</p>
            <div> {{ wsMessage }}</div>
        </div>
    </section>
</template>

<style>
#websockets {
    margin-top: 2rem;
    flex-grow: 1;
}
</style>