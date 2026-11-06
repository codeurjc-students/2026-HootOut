<script lang="ts" setup>
  import { nextTick, onMounted, onUnmounted, ref } from 'vue'

  const { items, loadMore } = defineProps<{
    items: Object
    loadMore: Function
  }>()

  const loaderRef = ref<HTMLElement | null>(null)
  const showLoader = ref<boolean>(true)
  let nextPage: string | null = null
  let loading = false

  const intersectionObserver = new IntersectionObserver(
    async (entries: IntersectionObserverEntry[]) => {
      if (!entries || entries.length == 0) return
      if (entries[0]!.intersectionRatio <= 0) return
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
    intersectionObserver.disconnect()
  })

  async function handleScroll(): Promise<void> {
    if (loading || !showLoader.value) return
    loading = true
    try {
      nextPage = await loadMore(nextPage)
    } catch (err) {
      // TODO: handle error. The cursor is kept, so the same page is retried
      // the next time the loader becomes visible.
      return
    } finally {
      loading = false
    }

    if (nextPage == null) {
      showLoader.value = false
      intersectionObserver.disconnect()
      return
    }
    await restartObserver()
  }

  async function restartObserver(): Promise<void> {
    await nextTick()
    const element = loaderRef.value
    if (!element) return
    intersectionObserver.unobserve(element)
    intersectionObserver.observe(element)
  }
</script>

<template>
  <div>
    <slot name="scroll-item" :item="item" v-for="item in items"></slot>
    <div class="mx-auto mt-4" v-if="showLoader" ref="loaderRef"></div>
  </div>
</template>
