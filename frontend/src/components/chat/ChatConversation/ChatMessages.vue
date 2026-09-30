<script setup lang="ts">
    import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import { onMounted, ref } from "vue";

const messages = ref(["test1", "test1"]);
onMounted(() => {
    wsMessageService.connect(() => {
        wsMessageService.subscribe("chat123", (payload: string) => {
            messages.value.push(payload);
        });
    }); 
});
</script>

<template>
    <div id="messages">
        <ul role="list" id="messages-list">
            <li class="message-entry" v-for='(message, index) in messages' :key='message'>
                {{ message }}
            </li>
        </ul>
    </div>
</template>

<style>
#messages { 
    max-height: inherit;
}

#messages-list {
    overflow-y: scroll;
    padding: 1rem;
    display: flex;
    flex-direction: column-reverse;
    max-height: inherit;
}

.message-entry  { 
}
</style>