<script setup lang="ts">
import navbarLogo from '@/assets/img/navbar-logo.svg'
import { userInfoStore } from '@/stores/userInfo';
import { storeToRefs } from 'pinia';

const userInfo = userInfoStore();

const { user } = storeToRefs(userInfo)

</script>

<template>
    <div id="main-layout" class="background-primary">
        <nav id="navbar">
            <img id="navbar-logo" :src="navbarLogo" alt="HootOutLogo" />

            <div id="navbar-user">
                {{ user.username }}
            </div>
        </nav>
        <section id="main-content">
            <slot />
        </section>
    </div>
</template>

<style lang="css">
#main-layout {
    width: 100vw;
    height: 100vh;
    max-height: 100vh;
    display: grid;
    grid-template-rows: var(--navbar-height) 1fr;
}

#navbar {
    display: flex;
    align-items: center;
    padding: 0 2rem;
}

#navbar-logo {
    height: 1.8rem;
}

#navbar-user {
    margin-left: auto;
}

@media(max-width: 520px) {
    #main-layout {
        max-width: 100vw;
    }

    #navbar {
        width: 100vw;
    }
}
</style>