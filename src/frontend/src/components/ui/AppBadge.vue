<script setup lang="ts">
interface Props {
  variant?: 'default' | 'success' | 'warning' | 'error' | 'info'
  colorClass?: string
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'default',
  colorClass: '',
})

const variantClasses: Record<string, string> = {
  default: 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
  success: 'bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300',
  warning: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300',
  error: 'bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300',
  info: 'bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300',
}
</script>

<template>
  <span
    :class="[
      'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
      props.colorClass || variantClasses[props.variant],
    ]"
  >
    <slot />
  </span>
</template>
