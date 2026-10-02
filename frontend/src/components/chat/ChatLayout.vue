<script setup lang="ts">
import ChatSideBar from './ChatSideBar.vue';
import ChatMainSection from './ChatMainSection.vue';
import wsMessageService from '@/services/common/websocketsMesage/WebSocketsMessageService.ts';
import { onMounted, provide } from 'vue';
import { userInfoStore } from '@/stores/userInfo.ts';

provide('wsMessageService', wsMessageService)

const userInfo = userInfoStore();

onMounted(() => {
    wsMessageService.connect(() => {}); 
    userInfo.setUsername(`User${Math.floor(Math.random() * 100000)}`);
})

</script>

<template>
    <section id="chat-layout">
        <ChatSideBar />
        <ChatMainSection />
    </section>
</template>

<style>
#chat-layout {
    display: grid;
    grid-template-columns: 100px 1fr;
    max-height: calc(100vh - 3rem);
    height: calc(100vh - 3rem);
}
</style>