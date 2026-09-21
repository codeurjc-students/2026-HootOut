<script setup>
import UserList from './UserList.vue';
import WsConection from './WsConnection.vue';

</script>

<template>
    <div class="temporal-data">
        <UserList />
        <WsConection />
    </div>

</template>

<style>
.temporal-data {
    display: flex;
    flex-direction: column;
    height: 100vh;
    padding: 2rem;
}

.temporal-data>* {
    background-color: var(--color-background-soft);
    border-radius: 20px;
    border-color: rgba(var(--color-border), 0.1);
    border: 1px solid;
    padding: 1rem;
}
</style>