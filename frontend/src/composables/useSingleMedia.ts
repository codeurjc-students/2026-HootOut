import { onMounted, onBeforeUnmount } from 'vue'

export function useSingleMedia() {
    const onPlay = (e: Event) => {
        const target = e.target
        if (!(target instanceof HTMLMediaElement)) return
        document.querySelectorAll<HTMLMediaElement>('audio, video').forEach((el) => {
            if (el !== target) el.pause()
        })
    }

    onMounted(() => document.addEventListener('play', onPlay, true))
    onBeforeUnmount(() => document.removeEventListener('play', onPlay, true))
}