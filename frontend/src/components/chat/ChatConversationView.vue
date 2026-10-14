<script setup lang="ts">

import ChatInfo from './chatConversation/ChatInfo.vue';
import ChatMessageBar from './chatConversation/ChatMessageBar.vue';
import ChatMessages from './chatConversation/ChatMessages.vue';

</script>

<template>
      <div id="chat" class="background-primary">
            <ChatInfo />
            <ChatMessages />
            <ChatMessageBar />
      </div>
</template>

<style>
#chat {
      display: grid;
      grid-template-rows: auto 1fr auto;
      border: 1px solid rgb(var(--color-border));
      max-height: inherit;
}
</style>