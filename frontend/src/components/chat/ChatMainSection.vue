<script setup lang="ts">
import ChatServerService from '@/services/chatServer/ChatServerService.ts';
import ChatConversationView from './ChatConversationView.vue';
import ChatLeftBar from './ChatLeftBar.vue';
import { serverInfoStore } from "@/stores/serverInfo.ts"
import { storeToRefs } from 'pinia';
import { watch } from 'vue';
import { useRoute } from 'vue-router';

const route = useRoute();
const serverInfo = serverInfoStore();
const { currentChatChannel } = storeToRefs(serverInfoStore())

watch(() => route.params.id,
    async newId => {
        if (serverInfo.servers.length == 0) {
            await serverInfo.getChatServers();
        }

        if (newId)
            serverInfo.setCurrentServer(newId as string);
    },
    {
        immediate: true
    }
)

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