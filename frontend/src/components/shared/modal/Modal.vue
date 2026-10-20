<script lang="ts" setup>
import { ref } from 'vue';

const props = defineProps({
    title: String,
    modalClass: String
})

const emit = defineEmits<{
    (e: 'close'): void
}>()

const show = ref(false)

function close() {
    show.value = false;
}

function open() {
    show.value = true;
}

defineExpose({
    close,
    open
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
                <div class="modal-close pointer" @click="close">
                    <span class="material-symbols-sharp">
                        close
                    </span>
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
    display: flex;
}

.modal-body {
    flex: 1;
    max-height: 100%;
}

.modal-close {
    margin-left: auto;
    display: flex;
}

.modal-close>span {
    display: inline-block;
    align-self: center;
}

@media(max-width: 520px) {
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

    .modal-close {
        top: 0;
        left: 0;
        order: -1;
        margin: 0;
    }

    .modal-title>h2 {
        margin: auto;
    }
}
</style>