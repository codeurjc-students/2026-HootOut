<script setup lang="ts">
import navbarLogo from '@/assets/img/navbar-logo.svg'
</script>

<template>
    <div id="main-layout">
        <nav id="navbar">
            <img id="navbar-logo" :src="navbarLogo" alt="HootOutLogo" />

            <div id="navbar-user">
                Username
            </div>
        </nav>
        <section id="content">
            <slot />
        </section>
    </div>
</template>

<style lang="css">
#main-layout {
    background-color: var(--color-background);
    width: 100vw;
    height: 100vh;
    max-height: 100vh;
    display: grid;
    grid-template-rows: 3rem 1fr;
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
</style>