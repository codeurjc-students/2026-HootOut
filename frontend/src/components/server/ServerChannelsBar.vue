<script setup lang="ts">
import { serverInfoStore } from '@/stores/serverInfo';
import { storeToRefs } from 'pinia';
import ChannelsList from '../channel/ChannelsList.vue';
import CreateChannel from '../channel/CreateChannel.vue';

const serverInfo = serverInfoStore();
const { currentServer } = storeToRefs(serverInfo) as any

</script>

<template>
    <div class="server-channels-bar">
        <div class="server-name p-1">
            <div> {{ currentServer?.name }}</div>
        </div>

        <CreateChannel />
        <ChannelsList />
    </div>

</template>

<style>
.server-name {
    font-size: 1.25rem;
    font-weight: bold;
    border-bottom: 1px solid rgb(var(--color-background-light));
}
</style>