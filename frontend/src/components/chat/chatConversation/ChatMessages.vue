<script setup lang="ts">
import { serverInfoStore } from "@/stores/serverInfo"
import { userInfoStore } from "@/stores/userInfo";
import { storeToRefs } from "pinia";
import { computed } from "vue";

const serverInfo = serverInfoStore();

const { channelMessages, currentChatChannel, pendingMessages } = storeToRefs(serverInfo);
const { user } = storeToRefs(userInfoStore());

const messages = computed(() => {
    return channelMessages.value[currentChatChannel.value!.id];
})

const pendingMessagesList = computed(() => {
    return pendingMessages?.value[currentChatChannel.value!.id];
});

function convertDate(date_string: string) {
    return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'short',
        timeStyle: 'short'
    }).format(new Date(date_string))
}

</script>

<template>
    <div id="messages-list">
        <div class="message-entry pending" v-for='(message, index) in pendingMessagesList' :key='message.id'
            :class="{ 'message-entry-user': message.author.username == user.username }">
            <div>
                {{ message.author.id }} --- {{ convertDate(message.createdAt!) }}
            </div>
            <div>
                {{ message.content }}
            </div>
        </div>

        <div class="message-entry" v-for='(message, index) in messages' :key='message.id'
            :class="{ 'message-entry-user': message.author.username == user.username }">
            <div>
                {{ message.author.username }} --- {{ convertDate(message.createdAt!) }}
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