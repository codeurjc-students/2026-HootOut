<script lang="ts" setup>
  import { ref } from 'vue'

  const props = defineProps({
    title: String,
    modalClass: String,
  })

  const emit = defineEmits<{
    (e: 'close'): void
  }>()

  const show = ref(false)

  function close() {
    show.value = false
  }

  function open() {
    show.value = true
  }

  defineExpose({
    close,
    open,
  })
</script>

<template>
  <Teleport to="body">
    <div
      v-if="show"
      class="modal-background background-primary fixed top-0 left-0 z-990 h-screen w-screen opacity-50"
      @click="close"
    ></div>
    <div
      v-if="show"
      class="modal custom-form-wrapper fixed top-0 left-0 z-999 flex max-h-screen min-h-screen max-w-screen min-w-screen flex-col overflow-y-auto p-8 md:top-[20%] md:left-[50%] md:min-h-[30vh] md:min-w-[30rem] md:-translate-x-[50%]"
      :class="modalClass"
    >
      <div class="modal-title grid grid-cols-[1fr_auto]">
        <h2 class="text-2xl md:mb-2">
          {{ props.title }}
        </h2>

        <div class="modal-options pointer ml-auto flex">
          <slot name="options"></slot>
          <div class="material-symbols-sharp modal-close" @click="close" aria-label="close">
            close
          </div>
        </div>
      </div>
      <div class="modal-body max-h-full flex-1" @keyup.esc="close">
        <slot name="body"></slot>
      </div>
    </div>
  </Teleport>
</template>

<style>
  .modal-options > span {
    display: inline-block;
    align-self: center;
  }

  @media (max-width: 520px) {
    .modal-options {
      top: 0;
      left: 0;
      order: -1;
      margin: 0;
    }

    .modal-close {
      order: -1;
    }

    .modal-title {
      grid-template-columns: 1fr;
      grid-template-rows: auto 1fr;
    }
  }
</style>
