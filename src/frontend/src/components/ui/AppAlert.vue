<script setup lang="ts">
import { ref } from 'vue'

interface Props {
  variant?: 'info' | 'warning' | 'error' | 'success'
  dismissible?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'info',
  dismissible: false,
})

const dismissed = ref(false)

const variantClasses: Record<string, string> = {
  info: 'bg-blue-50 text-blue-800 border-blue-200 dark:bg-blue-900/30 dark:text-blue-300 dark:border-blue-800',
  warning: 'bg-yellow-50 text-yellow-800 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-300 dark:border-yellow-800',
  error: 'bg-red-50 text-red-800 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  success: 'bg-green-50 text-green-800 border-green-200 dark:bg-green-900/30 dark:text-green-300 dark:border-green-800',
}
</script>

<template>
  <div
    v-if="!dismissed"
    :class="[
      'rounded-md border p-4',
      variantClasses[props.variant],
    ]"
    role="alert"
  >
    <div class="flex">
      <div class="flex-1">
        <slot />
      </div>
      <button
        v-if="props.dismissible"
        type="button"
        class="ml-3 inline-flex flex-shrink-0 rounded-md p-1.5 hover:opacity-70 focus:outline-none"
        @click="dismissed = true"
      >
        <span class="sr-only">Dismiss</span>
        <svg class="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
          <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
        </svg>
      </button>
    </div>
  </div>
</template>
