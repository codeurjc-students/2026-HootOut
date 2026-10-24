<script setup lang="ts">

import { serverInfoStore } from "@/stores/serverInfo"
import { storeToRefs } from "pinia";
import ServerChannelsBar from "../server/ServerChannelsBar.vue";

const { currentServer } = storeToRefs(serverInfoStore())

</script>

<template>
    <ServerChannelsBar v-if="currentServer" />
    <div v-else class="p-1">
        Select a Server First
    </div>

</template>