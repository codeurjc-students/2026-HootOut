<script setup lang="ts">
  import ChannelsList from '../channel/ChannelsList.vue'
  import CreateChannel from '../channel/CreateChannel.vue'
  import ServerName from './ServerName.vue'
</script>

<template>
  <div class="server-channels-bar">
    <ServerName />
    <CreateChannel />
    <ChannelsList />
  </div>
</template>
