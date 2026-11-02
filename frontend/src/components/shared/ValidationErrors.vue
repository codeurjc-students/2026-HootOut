<script setup lang="ts">
  const { errors } = defineProps({
    errors: Map<string, Map<string, string>>,
  })
</script>

<template>
  <div v-for="val in errors?.values()">
    <p v-for="err in val.values()" :key="err" class="custom-form-field-error">
      {{ err }}
    </p>
  </div>
</template>
