<script setup lang="ts">
</script>


<template>
      <section id="home">
            <div id="home-text">
                  <h1>HootOut</h1>

                  <h2>HootOut is an Instant Message application. <br> Connect with friends, make plans and chat with
                        them.
                  </h2>

                  <RouterLink to="/register">
                        Create an account and start chatting.
                  </RouterLink>
            </div>
            <div id="home-image">
                  <img src="/jeremy-hynes-cE6r38Mj7gA-unsplash.jpg" />
            </div>
      </section>
</template>

<style>
#home {
      display: flex;
      margin-top: 5rem;
      padding: 0 10vw;
}

#home>* {
      width: 50%;
      max-width: 50%;
}

#home-text {
      padding: 4rem;
      letter-spacing: .15em;
}

#home-text>h1 {
      font-size: 3rem;
      font-weight: 600;
}

#home-text>h2 {
      margin-top: 2rem;
}

#home-text>a {
      display: inline-block;
      font-size: 1.2rem;
      margin-top: 1rem;
      color: var(--color-text)
}

#home-image {
      align-self: center;
      max-width: 100%;
}

#home-image>img {
      width: auto;
      max-width: 100%;
      object-fit: contain;
      border-radius: 2rem;
      display: block;
}

@media(max-width: 520px) {
      #home {
            padding: 0;
            margin: 0;
            flex-direction: column;
            height: 100%;
      }

      #home>* {
            width: 100%;
            max-width: 100%;

      }

      #home-text {
            padding: 0 2rem;
            z-index: 1;
      }

      #home-text>h2 {
            margin-top: 1rem;
      }

      #home-image {
            opacity: 40%;
            margin-top: auto;
            padding-top: 2rem;
      }

      #home-image>img {
            border-bottom-left-radius: 0;
            border-bottom-right-radius: 0;
      }
}
</style>
