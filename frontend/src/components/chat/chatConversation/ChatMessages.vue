<script setup lang="ts">
import { serverInfoStore } from "@/stores/serverInfo"
import { userInfoStore } from "@/stores/userInfo";
import { storeToRefs } from "pinia";
import { computed } from "vue";

const serverInfo = serverInfoStore();

const { channelMessages, currentChatName, pendingMessages } = storeToRefs(serverInfo);
const { username } = storeToRefs(userInfoStore());

const messages = computed(() => {
    return channelMessages.value[currentChatName.value];
})

const pendingMessagesList = computed(() => {
    return pendingMessages?.value[currentChatName.value];
});

</script>

<template>
    <div id="messages-list">
        <div class="message-entry pending" v-for='(message, index) in pendingMessagesList' :key='message.id'
            :class="{ 'message-entry-user': message.author.username == username }">
            <div>
                {{ message.author.username }} --- {{ message.createdAt }}
            </div>
            <div>
                {{ message.content }}
            </div>
        </div>

        <div class="message-entry" v-for='(message, index) in messages' :key='message.id'
            :class="{ 'message-entry-user': message.author.username == username }">
            <div>
                {{ message.author.username }} --- {{ message.createdAt }}
            </div>
            <div>
                {{ message.content }}
            </div>
        </div>
    </div>
</template>

<style>
#messages-list {
    min-height: 100%;
    max-height: 100ch;
    overflow-y: auto;
    padding: 0 1rem;
    display: flex;
    flex-direction: column-reverse;
}

.message-entry-user {
    background-color: var(--color-background-soft);
}

.message-entry.pending {
    color: rgba(0, 0, 0, .3);
}
</style>