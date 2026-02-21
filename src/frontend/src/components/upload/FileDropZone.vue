<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDropZone } from '@vueuse/core'

const emit = defineEmits<{
  fileSelected: [file: File]
}>()

const { t } = useI18n()
const dropZoneRef = ref<HTMLDivElement>()
const fileInputRef = ref<HTMLInputElement>()

const { isOverDropZone } = useDropZone(dropZoneRef, {
  onDrop(files) {
    const first = files?.[0]
    if (first) {
      emit('fileSelected', first)
    }
  },
})

function onFileInputChange(event: Event) {
  const target = event.target as HTMLInputElement
  const first = target.files?.[0]
  if (first) {
    emit('fileSelected', first)
    target.value = ''
  }
}

function openFilePicker() {
  fileInputRef.value?.click()
}
</script>

<template>
  <div
    ref="dropZoneRef"
    :class="[
      'border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors',
      isOverDropZone
        ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
        : 'border-gray-300 dark:border-gray-600 hover:border-gray-400 dark:hover:border-gray-500',
    ]"
    @click="openFilePicker"
  >
    <input
      ref="fileInputRef"
      type="file"
      class="hidden"
      @change="onFileInputChange"
    />
    <svg class="mx-auto h-12 w-12 text-gray-400 dark:text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
    </svg>
    <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
      {{ isOverDropZone ? t('upload.dragActive') : t('upload.dragDrop') }}
    </p>
    <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">
      {{ t('upload.maxSize') }}
    </p>
  </div>
</template>
