<script setup lang="ts">
import ChatConversationView from './ChatConversationView.vue';
import ChatLeftBar from './ChatLeftBar.vue';
import { serverInfoStore } from "@/stores/serverInfo.ts"
import { storeToRefs } from 'pinia';

const { currentChatChannel } = storeToRefs(serverInfoStore())

</script>

<template>
    <section id="main-section" class="background-soft">
        <ChatLeftBar />
        <ChatConversationView v-if="currentChatChannel" />
        <div v-else id="empty-chat" class="background-primary p-1">
            Start a new chat
        </div>
    </section>
</template>

<style>
#main-section {
    border-radius: 30px 0 0 30px;
    display: grid;
    grid-template-columns: 300px 1fr;
    max-height: inherit;
}

#empty-chat {
    border: 1px solid rgb(var(--color-border));
}
</style>