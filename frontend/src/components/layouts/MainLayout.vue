<script setup lang="ts">
  import navbarLogo from '@/assets/img/navbar-logo.svg'
  import NavbarUser from '@/components/shared/navbar/NavbarUser.vue'
</script>

<template>
  <div
    id="main-layout"
    class="grid h-screen max-h-screen w-screen max-w-full grid-rows-[var(--navbar-height)_1fr] background-primary"
  >
    <nav id="navbar" class="flex w-screen items-center px-8 py-0">
      <img id="navbar-logo" class="h-7" :src="navbarLogo" alt="HootOutLogo" />

      <div id="navbar-user" class="ml-auto">
        <NavbarUser />
      </div>
    </nav>
    <section id="main-content">
      <slot />
    </section>
  </div>
</template>
