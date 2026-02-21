<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../../stores/review'
import EntitiesTabContent from './EntitiesTabContent.vue'

defineProps<{
  textSelection: ReturnType<typeof import('../../composables/useTextSelection').useTextSelection>
}>()

const { t } = useI18n()
const reviewStore = useReviewStore()
const confirmingDeleteAll = ref(false)

async function handleDeleteAll() {
  confirmingDeleteAll.value = false
  await reviewStore.deleteAllEntities()
}
</script>

<template>
  <div class="flex flex-col h-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 overflow-hidden">
    <!-- Header -->
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex-shrink-0">
      <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
        {{ t('review.workbench.zuordnung.title') }}
      </span>

      <!-- Delete all button -->
      <div v-if="reviewStore.entities.length > 0" class="flex items-center gap-1">
        <template v-if="!confirmingDeleteAll">
          <button
            class="text-xs text-red-500 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
            @click="confirmingDeleteAll = true"
          >
            {{ t('review.workbench.zuordnung.deleteAll') }}
          </button>
        </template>
        <template v-else>
          <button
            class="text-xs text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            @click="confirmingDeleteAll = false"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
          <button
            class="text-xs text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 font-medium"
            @click="handleDeleteAll"
          >
            {{ t('review.workbench.zuordnung.confirmDeleteAll') }}
          </button>
        </template>
      </div>
    </div>

    <!-- Scrollable content -->
    <div class="flex-1 overflow-y-auto p-4">
      <EntitiesTabContent :text-selection="textSelection" />
    </div>
  </div>
</template>
