<script setup lang="ts">
    const {imgUrl, alt} = defineProps<{imgUrl?: string | undefined, alt: string}>()
</script>

<template> 
    <div  >
        <img class="border-1 rounded-full object-cover h-full w-full" v-if="imgUrl" :src="imgUrl" alt="profile-picture"></img>
        <div v-else class="flex h-full justify-center self-center border-1 rounded-full"> <span class="server-btn-text inline-block self-center text-center ">
          {{ alt }}
        </span></div>
      </div>
</template>