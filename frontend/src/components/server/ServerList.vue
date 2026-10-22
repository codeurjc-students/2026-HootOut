<script setup lang="ts">
import { serverInfoStore } from '@/stores/serverInfo';
import type { ChatServer } from '@/types';
import { storeToRefs } from 'pinia';
import { onMounted } from 'vue';
import { useRouter } from 'vue-router';

const router = useRouter();

const serverInfo = serverInfoStore()
const { servers, currentServer } = storeToRefs(serverInfo)

function setCurrentServer(server: ChatServer) {
    router.push({ name: 'server', params: { serverId: server.id } })
}

</script>

<template>
    <div v-for="server in servers" class="round-btn server-btn pointer" @click="setCurrentServer(server)"
        :class="{ 'background-light': currentServer?.id == server.id }" :key="server.id">
        <span class="server-btn-text">
            {{ server.name.charAt(0).toUpperCase() }}
        </span>
    </div>

</template>

<style>
.server-btn {
    height: 100%;
    margin-bottom: .5rem;
    align-self: center;
    justify-content: center;
    display: flex;
}

.server-btn-text {
    display: inline-block;
    text-align: center;
    align-self: center;
    font-size: 2rem;
    transform: translateY(-2%);

}
</style>