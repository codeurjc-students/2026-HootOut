<script setup lang="ts">
import { serverInfoStore } from '@/stores/serverInfo';
import { storeToRefs } from 'pinia';


const { currentChatChannel } = storeToRefs(serverInfoStore())
</script>

<template>
    <div id="chat-info"> {{ currentChatChannel!.name }}</div>
</template>

<style>
#chat-info {
    background-color: var(--color-background-light);
    padding: .5rem;
    display: flex;
    align-items: center;
}
</style>