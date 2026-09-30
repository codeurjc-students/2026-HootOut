<script setup lang="ts">
    import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService"
import { onMounted, ref } from "vue";

const messages = ref(["test1", "test1","test1", "test1","test1", "test1","test1", "test1", "test1","test1", "test1","test1", "test1","test1", "test1","test1", "test1","test1", "test1", "test1","test1", "test1","test1", "test1","test1", "test1","test1", "test1", "test1","test1", "test1","test1", "test1","test1", "test1","test1", "test1"]);
onMounted(() => {
    wsMessageService.connect(() => {
        wsMessageService.subscribe("chat123", (payload: string) => {
            messages.value.push(payload);
        });
    }); 
});
</script>

<template>
    <div id="messages-list"> 
        <div class="message-entry" v-for='(message, index) in messages' :key='message'>
            {{ message }}
        </div> 
    </div>
</template>

<style>

#messages-list {
    min-height: 100%;
    max-height: 100ch;
    overflow-y: auto;
    padding: 1rem;
    display: flex;
    flex-direction: column-reverse; 
}

.message-entry  {

}
</style>