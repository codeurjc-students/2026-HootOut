<script setup lang="ts">
import ChatSideBar from './ChatSideBar.vue';
import ChatMainSection from './ChatMainSection.vue';


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
    height: 100%;
}
</style>