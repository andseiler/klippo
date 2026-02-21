<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../../stores/review'
import { getEntityTypeHighlightClass } from '../../constants/entityTypeColors'
import { useClipboardCopy } from '../../composables/useClipboardCopy'
import AppButton from '../ui/AppButton.vue'

const { t } = useI18n()
const reviewStore = useReviewStore()
const { copyText, justCopied } = useClipboardCopy()

function copyOutput() {
  copyText(reviewStore.depseudoOutputPlainText)
}
</script>

<template>
  <div class="space-y-4">
    <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.workbench.depseudo.mappingTitle') }}
    </h3>
    <p class="text-xs text-gray-500 dark:text-gray-400">
      {{ t('review.workbench.depseudo.mappingDescription') }}
    </p>

    <!-- Mapping table -->
    <div v-if="reviewStore.depseudoReplacementMap.size > 0" class="space-y-3">
      <table class="w-full text-xs">
        <thead>
          <tr class="border-b border-gray-200 dark:border-gray-700">
            <th class="text-left py-1.5 font-medium text-gray-500 dark:text-gray-400">
              {{ t('review.workbench.depseudo.tokenColumn') }}
            </th>
            <th class="text-left py-1.5 font-medium text-gray-500 dark:text-gray-400">
              {{ t('review.workbench.depseudo.originalColumn') }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="[token, entry] in reviewStore.depseudoReplacementMap"
            :key="token"
            class="border-b border-gray-100 dark:border-gray-800"
          >
            <td class="py-1.5 pr-2 font-mono text-gray-700 dark:text-gray-300">
              {{ token }}
            </td>
            <td class="py-1.5">
              <span
                :class="['rounded px-1 py-0.5 text-gray-900 dark:text-gray-100', getEntityTypeHighlightClass(entry.entityType)]"
              >{{ entry.original }}</span>
              <span class="ml-1 text-[10px] text-gray-400">{{ entry.entityType }}</span>
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Copy button -->
      <AppButton
        variant="primary"
        size="sm"
        class="w-full"
        :disabled="!reviewStore.depseudoInputText"
        @click="copyOutput"
      >
        {{ justCopied ? t('review.workbench.export.copied') : t('review.workbench.depseudo.copyOutput') }}
      </AppButton>
    </div>

    <!-- Empty state -->
    <p v-else class="text-xs text-gray-400 dark:text-gray-500 italic">
      {{ t('review.workbench.depseudo.noMappings') }}
    </p>
  </div>
</template>
