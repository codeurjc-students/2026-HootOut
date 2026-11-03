<script setup lang="ts">
  import PaginatedTable from '@/components/shared/table/PaginatedTable.vue'
</script>

<template>
  <div class="h-full w-full border-1 background-soft p-4">
    <h1 class="py- mb-4 text-2xl">Server Invitations</h1>
    <div class="h-[60%] max-h-[60%]">
      <PaginatedTable
        :headers="[
          { key: 'username', name: 'Username' },
          { key: 'status', name: 'Status' },
        ]"
        ,
        :query-function="() => {}"
      />
    </div>
  </div>
</template>
