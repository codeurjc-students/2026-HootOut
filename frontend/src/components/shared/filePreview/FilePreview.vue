<script setup lang="ts">

const { file } = defineProps({
    file: Object,
    filePreviewClass: String
})

</script>

<template>
    <img v-if="file?.contentType.startsWith('image')" :src="file.url" class="file-preview background-primary"
        :class="filePreviewClass"></img>

    <video v-else-if="file?.contentType.startsWith('video')" class="file-preview" :class="filePreviewClass"
        :src="file.url" controls preload="metadata" playsinline></video>

    <audio v-else-if="file?.contentType.startsWith('audio')" class="file-preview" :class="filePreviewClass" controls
        preload="metadata" playsinline>
        <source :src="file.url" :type="file.contentType">
    </audio>


    <div v-else class="generic-file-preview" :class="filePreviewClass">
        <div class="material-symbols-sharp">attach_file</div>
    </div>
    <div class="file-preview-name">
        {{ file?.fileName && file?.fileName.length > 20 ?
            (file?.fileName.slice(0, 16) + "...") :
            file?.fileName }}
    </div>
</template>

<style>
.file-preview,
.generic-file-preview {
    min-height: 128px;
    max-height: 128px;
    max-width: 128px;
    min-width: 128px;
    object-fit: cover;
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