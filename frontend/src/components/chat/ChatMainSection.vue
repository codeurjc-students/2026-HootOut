<script setup lang="ts">
import ChatConversationView from './ChatConversationView.vue';
import ChatLeftBar from './ChatLeftBar.vue';
import { serverInfoStore } from "@/stores/serverInfo.ts"
import { storeToRefs } from 'pinia';

const { currentChatChannel } = storeToRefs(serverInfoStore())

</script>

<template>
    <section id="main-section">
        <ChatLeftBar />
        <ChatConversationView v-if="currentChatChannel" />
        <div id="empty-chat" v-else>
            Start a new chat
        </div>
    </section>
</template>

<style>
#main-section {
    border-radius: 30px 0 0 30px;
    background-color: var(--color-background-soft);
    display: grid;
    grid-template-columns: 300px 1fr;
    max-height: inherit;
}

#empty-chat {
    padding: 1rem;
    background-color: var(--color-background);
    border: 1px solid var(--color-border);
}
</style>