<script setup lang="ts">
import { ProgressRoot, ProgressIndicator } from 'radix-vue'

interface Props {
  value: number
}

const props = defineProps<Props>()
</script>

<template>
  <ProgressRoot
    :model-value="props.value"
    class="relative h-3 w-full overflow-hidden rounded-full bg-gray-200 dark:bg-gray-700"
  >
    <ProgressIndicator
      class="h-full rounded-full bg-blue-600 dark:bg-blue-500 transition-all duration-300"
      :style="{ width: `${props.value}%` }"
    />
  </ProgressRoot>
</template>
