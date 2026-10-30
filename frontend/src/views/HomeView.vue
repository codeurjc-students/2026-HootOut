<script setup lang="ts"></script>

<template>
  <section id="home" class="flex h-full flex-col md:mt-20 md:h-auto md:flex-row">
    <div
      id="home-text"
      class="z-10 w-full max-w-full px-8 tracking-widest md:w-1/2 md:max-w-1/2 md:p-16"
    >
      <h1 class="text-5xl font-semibold">HootOut</h1>

      <h2 class="mt-4 text-2xl md:mt-8">
        <p>HootOut is an Instant Message application.</p>
        <p>Connect with friends, make plans and chat with them.</p>
      </h2>

      <RouterLink to="/register" class="color-text mt-4 inline-block text-xl underline">
        Create an account and start chatting.
      </RouterLink>
    </div>
    <div
      id="home-image"
      class="mt-auto w-full max-w-full self-center pt-8 opacity-50 md:mt-0 md:w-1/2 md:max-w-1/2 md:p-0 md:opacity-100"
    >
      <img
        class="block w-auto max-w-full rounded-4xl object-contain"
        src="/jeremy-hynes-cE6r38Mj7gA-unsplash.jpg"
      />
    </div>
  </section>
</template>

<style>
  #home {
    padding: 0 10vw;
  }

  @media (max-width: 520px) {
    #home {
      padding: 0;
    }
  }
</style>
