<script setup lang="ts">

const { file } = defineProps({
    file: Object
})


</script>

<template>
    <img v-if="file?.contentType.startsWith('image')" :src="file.url" class="img-preview background-primary"></img>

    <video v-else-if="file?.contentType.startsWith('video')" class="img-preview" :src="file.url" controls
        preload="metadata" playsinline></video>

    <div v-else class="generic-file-preview">
        <div class="material-symbols-sharp ">attach_file</div>
    </div>
    <div class="file-preview-name">
        {{ file?.fileName && file?.fileName.length > 20 ?
            (file?.fileName.slice(0, 16) + "...") :
            file?.fileName }}</div>
</template>

<style>
.img-preview,
.generic-file-preview {
    min-height: 128px;
    max-height: 128px;
    max-width: 128px;
    min-width: 128px;
    object-fit: contain;
    overflow: hidden;
}

.generic-file-preview {
    display: flex;
}

.generic-file-preview>div {
    margin: auto;
    font-size: 64px;
}

.file-preview-name {
    font-size: .9rem;
    position: absolute;
    bottom: 0;
    left: 0;
    padding: 0.2rem 0rem;
    text-align: center;
    width: 100%;
}
</style>