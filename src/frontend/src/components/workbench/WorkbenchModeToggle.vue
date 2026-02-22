<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { ViewMode } from '../../stores/review'

defineProps<{
  mode: ViewMode
}>()

const emit = defineEmits<{
  'update:mode': [value: ViewMode]
}>()

const { t } = useI18n()

const allModes: { value: ViewMode; labelKey: string }[] = [
  { value: 'pseudonymized', labelKey: 'review.workbench.modeToggle.verfremden' },
  { value: 'depseudonymized', labelKey: 'review.workbench.modeToggle.klartext' },
]
</script>

<template>
  <div class="inline-flex rounded-lg border border-gray-200 dark:border-gray-600 bg-gray-100 dark:bg-gray-700 p-1">
    <button
      v-for="m in allModes"
      :key="m.value"
      :class="[
        'px-3 py-1.5 md:px-5 md:py-2 text-sm font-semibold rounded-md transition-colors',
        mode === m.value
          ? 'bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 shadow-sm'
          : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300',
      ]"
      @click="emit('update:mode', m.value)"
    >
      {{ t(m.labelKey) }}
    </button>
  </div>
</template>
