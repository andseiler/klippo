<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const emit = defineEmits<{ confirm: [] }>()
const confirming = ref(false)

function handleConfirm() {
  confirming.value = false
  emit('confirm')
}

function handleCancel() {
  confirming.value = false
}
</script>

<template>
  <div class="flex-shrink-0 flex gap-0.5">
    <template v-if="confirming">
      <!-- Cancel icon (appears where trash was to prevent accidental double-click) -->
      <button
        class="p-0.5 rounded text-gray-500 hover:bg-gray-200 dark:hover:bg-gray-600"
        :title="t('review.cancelDelete')"
        @click.stop="handleCancel"
      >
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <!-- Confirm icon -->
      <button
        class="p-0.5 rounded text-red-600 hover:bg-red-100 dark:hover:bg-red-900/30"
        :title="t('review.confirmDelete')"
        @click.stop="handleConfirm"
      >
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
      </button>
    </template>
    <template v-else>
      <!-- Trash icon -->
      <button
        class="p-0.5 rounded text-gray-400 hover:text-red-600 hover:bg-red-100 dark:hover:bg-red-900/30"
        :title="t('common.delete')"
        @click.stop="confirming = true"
      >
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
        </svg>
      </button>
    </template>
  </div>
</template>
