<script setup lang="ts"></script>

<template>
  <div class="flex h-full flex-col justify-items-center p-8 md:items-center">
    <h1 class="mt-8 text-5xl">404 Page not found.</h1>
    <h2 class="mt-4 text-2xl">The page you requested cannot be found.</h2>
    <h3 class="mt-4 text-xl">
      <RouterLink :class="'underline!'" :to="{ name: 'home' }">Return to Home</RouterLink>
    </h3>
  </div>
</template>
