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
    <div v-if="show" class="modal-background" @click="close"></div>
    <div v-if="show" class="modal custom-form-wrapper p-2" :class="modalClass">
      <div class="modal-title">
        <h2>
          {{ props.title }}
        </h2>

        <div class="modal-top pointer">
          <slot name="options"></slot>
          <div class="material-symbols-sharp modal-close" @click="close" aria-label="close">
            close
          </div>
        </div>
      </div>
      <div class="modal-body" @keyup.esc="close">
        <slot name="body"></slot>
      </div>
    </div>
  </Teleport>
</template>

<style>
  .modal-background {
    position: fixed;
    top: 0;
    left: 0;
    width: 100vw;
    height: 100vh;
    z-index: 998;
    background-color: rgba(var(--color-background), 0.5);
  }

  .modal {
    min-width: 30rem;
    min-height: 30vh;
    position: fixed;
    z-index: 999;
    top: 20%;
    left: 50%;
    transform: translateX(-50%);
    display: flex;
    flex-direction: column;
  }

  .modal-title {
    display: grid;
    grid-template-columns: 1fr auto;
  }

  .modal-body {
    flex: 1;
    max-height: 100%;
  }

  .modal-top {
    margin-left: auto;
    display: flex;
  }

  .modal-top > span {
    display: inline-block;
    align-self: center;
  }

  @media (max-width: 520px) {
    .modal {
      top: 0;
      left: 0;
      transform: unset;
      min-width: 100vw;
      max-width: 100vw;
      min-height: 100vh;
      max-height: 100vh;
      overflow-y: auto;
    }

    .modal-top {
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
