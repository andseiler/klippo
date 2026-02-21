<script setup lang="ts">
import {
  SelectRoot,
  SelectTrigger,
  SelectValue,
  SelectPortal,
  SelectContent,
  SelectViewport,
  SelectItem,
  SelectItemText,
  SelectItemIndicator,
} from 'radix-vue'

interface Option {
  value: string
  label: string
}

interface Props {
  modelValue: string
  options: Option[]
  placeholder?: string
}

defineProps<Props>()
defineEmits<{
  'update:modelValue': [value: string]
}>()
</script>

<template>
  <SelectRoot :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)">
    <SelectTrigger
      class="inline-flex items-center justify-between gap-2 rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm text-gray-900 dark:text-gray-100 hover:bg-gray-50 dark:hover:bg-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 min-w-[160px]"
    >
      <SelectValue :placeholder="placeholder" />
      <svg class="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
      </svg>
    </SelectTrigger>
    <SelectPortal>
      <SelectContent
        class="z-50 min-w-[160px] overflow-hidden rounded-md border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-700 shadow-lg"
        position="popper"
        :side-offset="4"
      >
        <SelectViewport class="p-1">
          <SelectItem
            v-for="option in options"
            :key="option.value"
            :value="option.value"
            class="relative flex cursor-pointer items-center rounded-md px-8 py-2 text-sm text-gray-900 dark:text-gray-100 hover:bg-gray-100 dark:hover:bg-gray-600 focus:bg-gray-100 dark:focus:bg-gray-600 outline-none data-[highlighted]:bg-gray-100 dark:data-[highlighted]:bg-gray-600"
          >
            <SelectItemIndicator class="absolute left-2 inline-flex items-center">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
              </svg>
            </SelectItemIndicator>
            <SelectItemText>{{ option.label }}</SelectItemText>
          </SelectItem>
        </SelectViewport>
      </SelectContent>
    </SelectPortal>
  </SelectRoot>
</template>
