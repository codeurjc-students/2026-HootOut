<script setup lang="ts">
  import type { MessageAttachment } from '@/types'
  import FilePreview from './FilePreview.vue'
  import Modal from '../modal/Modal.vue'
  import { computed, ref, useTemplateRef } from 'vue'
  import { getBlob } from '@/services/common/api/ApiService.ts'

  const { files, canRemove } = defineProps({
    files: Array<any>,
    canRemove: {
      type: Boolean,
      default: false,
    },
  })

  const modalFilePreview = useTemplateRef('view-file-modal')
  const modalFile = ref<MessageAttachment>()

  const emit = defineEmits<{
    (e: 'remove', file: MessageAttachment): void
  }>()

  function openFile(file: MessageAttachment): void {
    modalFile.value = file
    modalFilePreview.value?.open()
  }

  function closePreview(): void {
    modalFile.value = undefined
  }

  const modalFileTitle = computed(() =>
    modalFile?.value?.fileName && modalFile?.value?.fileName.length > 60
      ? modalFile?.value?.fileName.slice(0, 60) + '...'
      : modalFile?.value?.fileName
  )
</script>

<template>
  <div class="file-preview-list flex w-full gap-4 overflow-auto p-4">
    <div
      v-for="file in files"
      class="file-preview-entry background-soft pointer relative flex justify-center rounded-lg border-1 p-4"
      :key="file.id"
      @click="openFile(file)"
    >
      <FilePreview :file="file" />
      <div
        v-if="canRemove"
        class="material-symbols-sharp pointer inline-btn file-preview-close-btn absolute top-0 right-0"
        @click.stop="emit('remove', file)"
      >
        close
      </div>
    </div>
    <Modal
      :title="modalFileTitle"
      ref="view-file-modal"
      @close="closePreview"
      modalClass="w-[80vw]! max-w-[80vw]! h-[80vh]! md:top-[10vh]!"
    >
      <template v-slot:options>
        <a
          class="material-symbols-sharp file-download px-2"
          :href="modalFile?.downloadUrl"
          download
        >
          download
        </a>
      </template>
      <template v-slot:body>
        <FilePreview
          :file="modalFile"
          filePreviewClass="h-full! max-h-[95%]! max-w-full! object-contain! justify-self-center"
        />
      </template>
    </Modal>
  </div>
</template>

<style>
  .file-preview-close-btn {
    color: rgb(var(--color-warning-light));
  }

  .file-preview-entry:hover::before {
    content: '\A';
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(var(--color-background-light), 0.2);
    pointer-events: none;
  }
</style>
