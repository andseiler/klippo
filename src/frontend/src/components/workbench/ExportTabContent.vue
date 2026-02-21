<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../../stores/review'
import TokenMappingTable from './TokenMappingTable.vue'
import AddPiiByTextPanel from './AddPiiByTextPanel.vue'
import AppButton from '../ui/AppButton.vue'

const { t } = useI18n()
const reviewStore = useReviewStore()
const copied = ref(false)

async function copyToClipboard() {
  await navigator.clipboard.writeText(reviewStore.pseudonymizedFullText)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}
</script>

<template>
  <div class="space-y-4">
    <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.workbench.tabs.export') }}
    </h3>
    <p class="text-xs text-gray-500 dark:text-gray-400">
      {{ t('review.workbench.export.description') }}
    </p>

    <AppButton variant="primary" size="sm" class="w-full" @click="copyToClipboard">
      {{ copied ? t('review.workbench.export.copied') : t('review.workbench.export.copyText') }}
    </AppButton>

    <!-- Add PII by text -->
    <AddPiiByTextPanel />

    <!-- Token mapping table (with delete) -->
    <div>
      <h4 class="text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
        {{ t('review.workbench.export.mappingTitle') }}
      </h4>
      <p class="text-xs text-gray-500 dark:text-gray-400 mb-2">
        {{ t('review.workbench.export.mappingDescription') }}
      </p>
      <TokenMappingTable :show-delete="true" />
    </div>
  </div>
</template>
