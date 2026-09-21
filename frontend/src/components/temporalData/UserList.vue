<script setup type="ts">
import { onMounted, ref } from 'vue';
import UserService from '@/services/users/UserService';

const users = ref([]);

const error = ref(false)

onMounted(async () => {
    try {
        users.value = [...await UserService.getAllUsers()];
    } catch (ex) {
        error.value = true
    }
});

</script>


<template>
    <section v-if="!error" id='user-list'>
        <h2>User List:</h2>
        <ul role="list" id="user-list">
            <li v-for='(user, index) in users' :key='user.uid'>
                {{ index }}: ID: {{ user.uid }} Username: {{ user.username }} Password: {{ user.password }}
            </li>
        </ul>
    </section>
    <section v-else>
        Error
    </section>
</template>

<style>
#user-list {
    flex: 0 0 60%;
    overflow-y: auto;
}
</style>