<script setup>
import ChatInfo from './ChatConversation/ChatInfo.vue';
import ChatMessageBar from './ChatConversation/ChatMessageBar.vue';
import ChatMessages from './ChatConversation/ChatMessages.vue';


</script>

<template>
      <div id="chat">
            <ChatInfo />
            <ChatMessages />
            <ChatMessageBar />
      </div>
</template>

<style>
#chat {
      display: grid;
      grid-template-rows: auto 1fr auto;
      background-color: var(--color-background);
      border: 1px solid var(--color-border);
}
</style>