<script setup lang="ts"> 
import { serverInfoStore } from "@/stores/serverInfo" 
import { storeToRefs } from "pinia";
import { computed } from "vue";

const serverInfo = serverInfoStore();

const {channelMessages, currentChatName} = storeToRefs(serverInfo);

const messages = computed(() => {
    return channelMessages.value[currentChatName.value];
})

</script>

<template>
    <div id="messages-list">
        <div class="message-entry" v-for='(message, index) in messages' :key='message.id'>
            <div>
                {{ message.author.username }} --- {{ message.createdAt }}
            </div>
            <div>
                {{ message.content }}
            </div>
        </div>
    </div>
</template>

<style>
#messages-list {
    min-height: 100%;
    max-height: 100ch;
    overflow-y: auto;
    padding: 0 1rem;
    display: flex;
    flex-direction: column-reverse;
}

.message-entry {}
</style>