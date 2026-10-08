<script setup lang="ts">
import ChatSideBar from './ChatSideBar.vue';
import ChatMainSection from './ChatMainSection.vue';
import { onMounted } from 'vue';
import { userInfoStore } from '@/stores/userInfo.ts';
import type { UserInfo } from '@/types/userInfo.ts';
import { get } from '@/services/common/api/ApiService.ts';


const userInfo = userInfoStore();

onMounted(async () => {
    const user = await get<UserInfo>('users/me');
    userInfo.setUser(user);
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