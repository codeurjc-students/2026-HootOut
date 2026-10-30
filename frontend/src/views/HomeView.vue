<script setup lang="ts">
</script>

<template>
      <section id="home" class="flex flex-col md:flex-row h-full md:h-auto md:mt-20">
            <div id="home-text" class="w-full max-w-full md:w-1/2 md:max-w-1/2 px-8 md:p-16 tracking-widest z-10">
                  <h1 class="text-5xl font-semibold">HootOut</h1>

                  <h2 class="text-2xl mt-4 md:mt-8">
                        <p>HootOut is an Instant Message application.</p>
                        <p> Connect with friends, make plans and chat with them.</p>
                  </h2>

                  <RouterLink to="/register" class="color-text inline-block text-xl underline mt-4">
                        Create an account and start chatting.
                  </RouterLink>
            </div>
            <div id="home-image"
                  class="w-full max-w-full md:w-1/2 md:max-w-1/2 self-center opacity-50 md:opacity-100 mt-auto md:mt-0 pt-8 md:p-0">
                  <img class="w-auto max-w-full object-contain rounded-4xl block"
                        src="/jeremy-hynes-cE6r38Mj7gA-unsplash.jpg" />
            </div>
      </section>
</template>

<style>
#home {
      padding: 0 10vw;
}

@media(max-width: 520px) {
      #home {
            padding: 0;
      }

}
</style>
