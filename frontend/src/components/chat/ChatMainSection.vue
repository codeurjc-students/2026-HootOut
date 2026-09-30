<script setup>
import ChatConversation from './ChatConversation.vue';
import ChatLeftBar from './ChatLeftBar.vue';

</script>

<template>
    <section id="main-section">
        <ChatLeftBar />
        <ChatConversation />
    </section>
</template>

<style>
#main-section {
    border-radius: 30px 0 0 30px;
    background-color: var(--color-background-soft); 
    display: grid;
    grid-template-columns: 250px 1fr;
    max-height: inherit;
}
</style>