<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import MainLayout from './components/layouts/MainLayout.vue';
import NoLayout from './components/layouts/NoLayout.vue';
import HomeLayout from './components/layouts/HomeLayout.vue';
import { computed } from 'vue';

const layouts = {
    MainLayout,
    NoLayout,
    HomeLayout
}

const route = useRoute()

const currentLayout = computed(() => {
    const layoutName = (route.meta.layout || 'NoLayout') as keyof typeof layouts
    return layouts[layoutName];
});

</script>

<template>
    <component :is="currentLayout">
        <RouterView v-slot="{ Component }">
            <component :is="Component" :key="$route.fullPath" />
        </RouterView>
    </component>
</template>
