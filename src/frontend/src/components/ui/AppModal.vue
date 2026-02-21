<script setup lang="ts">
import {
  DialogRoot,
  DialogTrigger,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogClose,
} from 'radix-vue'

interface Props {
  open: boolean
  title: string
  persistent?: boolean
  size?: 'default' | 'wide'
}

const props = withDefaults(defineProps<Props>(), {
  persistent: false,
  size: 'default',
})

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

function handleOpenChange(value: boolean) {
  if (props.persistent && !value) return
  emit('update:open', value)
}
</script>

<template>
  <DialogRoot :open="open" @update:open="handleOpenChange">
    <DialogTrigger v-if="$slots.trigger" as-child>
      <slot name="trigger" />
    </DialogTrigger>
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-40 bg-black/50" />
      <DialogContent
        :class="[
          'fixed left-1/2 top-1/2 z-50 w-full -translate-x-1/2 -translate-y-1/2 rounded-lg bg-white dark:bg-gray-800 p-6 shadow-xl max-h-[90vh] flex flex-col',
          size === 'wide' ? 'max-w-2xl' : 'max-w-lg',
        ]"
      >
        <div class="flex items-center justify-between mb-4 flex-shrink-0">
          <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-gray-100">
            {{ title }}
          </DialogTitle>
          <DialogClose
            v-if="!persistent"
            class="rounded-md p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 focus:outline-none"
          >
            <svg class="h-5 w-5" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
            </svg>
          </DialogClose>
        </div>
        <div class="overflow-y-auto flex-1">
          <slot />
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
