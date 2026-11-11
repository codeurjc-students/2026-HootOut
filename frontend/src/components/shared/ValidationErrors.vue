<script setup lang="ts">
  const { errors } = defineProps({
    errors: Map<string, Map<string, string>>,
  })
</script>

<template>
  <div v-for="key in errors?.keys()" :key="key">
    <p v-for="err in errors?.get(key)?.values() ?? []" :key="err" class="custom-form-field-error">
      {{ err }}
    </p>
  </div>
</template>
