<script setup lang="ts">
import { serverInfoStore } from '@/stores/serverInfo';
import type { ChatChannel } from '@/types';
import { storeToRefs } from 'pinia';

const serverInfo = serverInfoStore();
const { channels, currentChatChannel } = storeToRefs(serverInfo) as any

async function selectChannel(channel: ChatChannel) {
    serverInfo.selectChannel(channel);
}
</script>

<template>
    <div class="channel-list">
        <div v-for="channel in channels" class="channel-name pointer"
            :class="{ 'background-light': channel.id == currentChatChannel?.id }" :key="channel.id"
            @click="selectChannel(channel)">
            {{ channel.name }}
        </div>
    </div>
</template>

<style>
.channel-name {
    padding: 0.25rem 1rem;
    border-bottom: 1px solid rgb(var(--color-background-light));
}

.channel-name:hover {
    background-color: rgb(var(--color-background-light));
}
</style>