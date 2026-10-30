<script setup lang="ts"></script>

<template>
  <div class="flex h-full flex-col justify-items-center p-8 md:items-center">
    <h1 class="mt-8">404 Page not found.</h1>
    <h2 class="mt-4">The page you requested cannot be found.</h2>
    <h3 class="mt-4">
      <RouterLink :class="'underline!'" :to="{ name: 'home' }">Return to Home</RouterLink>
    </h3>
  </div>
</template>
