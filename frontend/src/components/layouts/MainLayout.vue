<script setup lang="ts">
  import navbarLogo from '@/assets/img/navbar-logo.svg'
  import { userInfoStore } from '@/stores/userInfo'
  import { storeToRefs } from 'pinia'

  const userInfo = userInfoStore()

  const { user } = storeToRefs(userInfo)
</script>

<template>
  <div
    id="main-layout"
    class="background-primary grid h-screen max-h-screen w-screen max-w-full grid-rows-[var(--navbar-height)_1fr]"
  >
    <nav id="navbar" class="flex w-screen items-center px-8 py-0">
      <img id="navbar-logo" class="h-7" :src="navbarLogo" alt="HootOutLogo" />

      <div id="navbar-user" class="ml-auto">
        {{ user.username }}
      </div>
    </nav>
    <section id="main-content">
      <slot />
    </section>
  </div>
</template>
