<script setup lang="ts">
import type { MessageAttachment } from '@/types';
import FilePreview from './FilePreview.vue';
import Modal from '../modal/Modal.vue';
import { ref, useTemplateRef } from 'vue';

const { files, canRemove } = defineProps({
    files: Array<any>,
    canRemove: {
        type: Boolean,
        default: false
    }
})

const modalFilePreview = useTemplateRef('view-file-modal')
const modalFile = ref<MessageAttachment>()

const emit = defineEmits<{
    (e: 'remove', file: MessageAttachment): void
}>()

function openFile(file: MessageAttachment): void {
    modalFile.value = file;
    modalFilePreview.value?.open();
}

function closePreview(): void {
    modalFile.value = undefined;
}

</script>

<template>
    <div class="file-preview-list p-1">
        <div v-for="file in files" class="file-preview-entry background-soft pointer" :key="file.id"
            @click="openFile(file)">
            <FilePreview :file="file" />
            <div v-if="canRemove" class="material-symbols-sharp pointer inline-btn file-preview-close-btn"
                @click.stop="emit('remove', file)">close
            </div>
        </div>
        <Modal :title="modalFile?.fileName" ref="view-file-modal" @close="closePreview" modalClass="file-preview-modal">
            <template v-slot:body>
                <FilePreview :file="modalFile" filePreviewClass="modal-file-preview" />
            </template>
        </Modal>
    </div>
</template>

<style>
.file-preview-list {
    display: flex;
    width: 100%;
    overflow: auto;
    gap: 1rem;
}

.file-preview-entry {
    display: flex;
    justify-content: center;
    border: 1px solid rgb(var(--color-border));
    border-radius: .5rem;
    padding: 1rem;
    position: relative;
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

.file-preview-modal {
    width: 80vw;
    max-width: 80vw;
    height: 80vh;
    top: 10vh;
}

@media(max-width: 520px) {
    .file-preview-modal {
        top: 0;
    }
}

.modal-file-preview {
    height: 100%;
    max-height: 100%;
    max-width: 100%;
    object-fit: contain;
}
</style>