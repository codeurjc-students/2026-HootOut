<script setup lang="ts">
import FilePreview from "@/components/shared/filePreview/FilePreview.vue";
import FilePreviewList from "@/components/shared/filePreview/FilePreviewList.vue";
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
        <div class="message-entry pending" v-for='(message, index) in pendingMessagesList' :key='message.id'>
            <div>
                {{ user.username }} --- {{ convertDate(message.createdAt!) }}
            </div>
            <div>
                {{ message.content }}
            </div>
        </div>

        <div v-for='(message, index) in messages' :key='message.id'
            :class="{ 'background-soft': message.author.username == user.username }">
            <div>
                {{ message.author.username }} --- {{ convertDate(message.createdAt!) }}
            </div>

            <FilePreviewList v-if="message.attachments.length > 0" :files="message.attachments" />
            <div>
                {{ message.content }}
            </div>
        </div>
    </div>
</template>

<style>
#messages-list {
    min-height: 100%;
    overflow-y: auto;
    padding: 0 1rem;
    display: flex;
    flex-direction: column-reverse;
}

.message-entry.pending {
    opacity: 0.3;
}
</style>