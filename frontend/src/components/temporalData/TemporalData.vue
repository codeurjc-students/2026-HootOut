<script setup>
import { onMounted, ref } from 'vue';
import userService from '../../services/users/UserService'

let users = ref([]);
let wsStatus = ref('');
let wsMessage = ref('');
let message = ref('');

onMounted(async () => {
    users.value = [...await userService.getAllUsers()];
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
        let data = message.value;
        this.socket.send(data);
    }
}

function sendMessage() {
    websocketsService.sendMessage(message);
}

</script>

<template>
    <div class="temporal-data">
        <section id='user-list'>
            <h2>User List:</h2>
            <ul>
                <li v-for='user in users' :key='user.Uid'>
                    ID: {{ user.uid }} Username: {{ user.username }} Password: {{ user.password }}
                </li>
            </ul>
        </section>
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
    </div>

</template>

<style>
.temporal-data {
    display: flex;
    flex-direction: column;
    height: 100vh;
    padding: 2rem;
}

.temporal-data>* {
    background-color: var(--color-background-soft);
    border-radius: 20px;
    border-color: rgba(var(--color-border), 0.1);
    border: 1px solid;
    padding: 1rem;
}

#user-list {
    flex: 0 0 60%;
    overflow-y: auto;
}

#websockets {
    margin-top: 2rem;
    flex-grow: 1;
}
</style>