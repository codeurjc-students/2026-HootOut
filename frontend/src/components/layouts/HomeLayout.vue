<script setup lang="ts">
  import navbarLogo from '@/assets/img/navbar-logo.svg'
  import { RouterLink } from 'vue-router'
</script>

<template>
  <div
    id="home-layout"
    class="mx-auto my-0 grid h-screen max-h-screen w-screen grid-rows-[5rem_1fr]"
  >
    <nav id="home-navbar" class="background-primary">
      <RouterLink to="/" class="pointer">
        <img id="home-navbar-logo" :src="navbarLogo" alt="HootOutLogo" />
      </RouterLink>

      <div id="login-register-btns">
        <RouterLink to="/login" class="button-action"> Login </RouterLink>
        <RouterLink to="/register" class="button-action"> Register </RouterLink>
      </div>
    </nav>
    <section id="home-content">
      <slot />
    </section>
  </div>
</template>

<style lang="css">
  #home-navbar {
    display: flex;
    align-items: center;
    align-content: space-between;
    padding: 0 10vw;
    max-height: 5rem;
  }

  #home-navbar-logo {
    height: 2.5rem;
  }

  #home-content {
    height: 100%;
  }

  #login-register-btns {
    margin-left: auto;
    display: flex;
    gap: 1rem;
    align-items: center;
  }
</style>
