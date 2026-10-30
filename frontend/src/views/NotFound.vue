<script setup lang="ts">
</script>


<template>
    <div class="h-full flex flex-col md:items-center justify-items-center p-8 ">
        <h1 class="mt-8">
            404 Page not found.
        </h1>
        <h2 class="mt-4">
            <p>The page you requested cannot be found.</p>
        </h2>
        <h3 class="mt-4">
            <RouterLink :class="'underline!'" :to="{ name: 'home' }">Return to Home</RouterLink>
        </h3>
    </div>
</template>