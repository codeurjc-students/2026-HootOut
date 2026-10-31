<script setup lang="ts">
  import navbarLogo from '@/assets/img/navbar-logo.svg'
  import { useAuthStore } from '@/stores/auth'
  import { storeToRefs } from 'pinia'
  import { RouterLink } from 'vue-router'

  const auth = useAuthStore()
  const { isAuthenticated } = storeToRefs(auth)
</script>

<template>
  <div
    id="home-layout"
    class="mx-auto my-0 grid h-screen max-h-screen w-screen grid-rows-[5rem_1fr]"
  >
    <nav class="background-primary flex max-h-20 content-between items-center px-[10vw]">
      <RouterLink to="/" class="pointer">
        <img id="home-navbar-logo" class="h-10" :src="navbarLogo" alt="HootOutLogo" />
      </RouterLink>

      <div v-if="!isAuthenticated" id="login-register-btns" class="ml-auto flex items-center gap-4">
        <RouterLink to="/login" class="button-action"> Login </RouterLink>
        <RouterLink to="/register" class="button-action"> Register </RouterLink>
      </div>
      <RouterLink :to="{ name: 'chat' }" v-else class="button-action ml-auto"
        >Open HootOut</RouterLink
      >
    </nav>
    <section id="home-content" class="h-full">
      <slot />
    </section>
  </div>
</template>
