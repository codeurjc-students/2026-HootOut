<script lang="ts" setup>
  import { onMounted, onUnmounted, ref } from 'vue'

  const { items, loadMore } = defineProps<{
    items: Object
    loadMore: Function
  }>()

  const loaderRef = ref<HTMLElement | null>(null)
  const showLoader = ref<boolean>(true)
  let nextPage: string | null = null

  const intersectionObserver = new IntersectionObserver(
    async (entries: IntersectionObserverEntry[]) => {
      if (!entries || entries.length == 0) return
      if (entries[0]!.intersectionRatio <= 0) return
      entries.forEach((entry: IntersectionObserverEntry) => {})
      await handleScroll()
    },
    { threshold: 0.5 }
  )

  onMounted(() => {
    const element = loaderRef.value
    if (!element) return
    intersectionObserver.observe(element)
  })

  onUnmounted(() => {
    const element = loaderRef.value
    if (!element) return
    intersectionObserver.disconnect()
  })

  async function handleScroll(): Promise<void> {
    nextPage = await loadMore(nextPage)
    if (nextPage == null) {
      showLoader.value = false
    }
  }
</script>

<template>
  <div>
    <slot name="scroll-item" :item="item" v-for="item in items"></slot>
    <div class="mx-auto mt-4" v-if="showLoader" ref="loaderRef"></div>
  </div>
</template>
