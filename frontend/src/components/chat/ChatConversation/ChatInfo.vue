<script setup>
import { serverInfoStore } from '@/stores/serverInfo';
import { storeToRefs } from 'pinia';


const { currentChatName } = storeToRefs(serverInfoStore())
</script>

<template>
    <div id="chat-info"> {{ currentChatName }}</div>
</template>

<style>
#chat-info {
    background-color: var(--color-background-light);
    padding: .5rem;
    display: flex;
    align-items: center;
}
</style>