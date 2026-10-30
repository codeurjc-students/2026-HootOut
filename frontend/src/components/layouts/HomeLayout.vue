<script setup lang="ts">
  import navbarLogo from '@/assets/img/navbar-logo.svg'
  import { RouterLink } from 'vue-router'
</script>

<template>
  <div
    id="home-layout"
    class="mx-auto my-0 grid h-screen max-h-screen w-screen grid-rows-[5rem_1fr]"
  >
    <nav id="home-navbar" class="background-primary flex max-h-20 content-between items-center">
      <RouterLink to="/" class="pointer">
        <img id="home-navbar-logo" class="h-10" :src="navbarLogo" alt="HootOutLogo" />
      </RouterLink>

      <div id="login-register-btns" class="ml-auto flex items-center gap-4">
        <RouterLink to="/login" class="button-action"> Login </RouterLink>
        <RouterLink to="/register" class="button-action"> Register </RouterLink>
      </div>
    </nav>
    <section id="home-content" class="h-full">
      <slot />
    </section>
  </div>
</template>

<style lang="css">
  #home-navbar {
    padding: 0 10vw;
  }
</style>
