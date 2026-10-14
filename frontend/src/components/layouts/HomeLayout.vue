<script setup lang="ts">
import navbarLogo from '@/assets/img/navbar-logo.svg'
import { RouterLink } from 'vue-router';

</script>

<template>
    <div id="home-layout">
        <nav id="navbar" class="background-primary">
            <RouterLink to="/" class="pointer">
                <img id="navbar-logo" :src="navbarLogo" alt="HootOutLogo" />
            </RouterLink>

            <div id="login-register-btns">
                <RouterLink to="/login" class="button-action"> Login </RouterLink>
                <RouterLink to="/register" class="button-action"> Register </RouterLink>
            </div>
        </nav>
        <section id="content">
            <slot />
        </section>
    </div>
</template>

<style lang="css">
#home-layout {
    width: 100vw;
    height: 100vh;
    max-height: 100vh;
    margin: 0 auto;
    display: grid;
    grid-template-rows: 5rem 1fr;
}

#navbar {
    display: flex;
    align-items: center;
    align-content: space-between;
    padding: 0 10vw;
    max-height: 5rem;
}

#navbar-logo {
    height: 2.5rem;
}

#content {
    height: 100%;
}

#login-register-btns {
    margin-left: auto;
    display: flex;
    gap: 1rem;
    align-items: center;
}
</style>