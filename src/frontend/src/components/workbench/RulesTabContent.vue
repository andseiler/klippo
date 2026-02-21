<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="space-y-4">
    <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.workbench.rules.title') }}
    </h3>
    <p class="text-xs text-gray-500 dark:text-gray-400">
      {{ t('review.workbench.rules.description') }}
    </p>

    <!-- Detection toggles (display-only) -->
    <div class="space-y-2">
      <div class="flex items-center justify-between py-2 px-3 rounded-md bg-gray-50 dark:bg-gray-800">
        <span class="text-sm text-gray-700 dark:text-gray-300">{{ t('review.workbench.rules.nerToggle') }}</span>
        <span class="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded-full bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300">ON</span>
      </div>
      <div class="flex items-center justify-between py-2 px-3 rounded-md bg-gray-50 dark:bg-gray-800">
        <span class="text-sm text-gray-700 dark:text-gray-300">{{ t('review.workbench.rules.regexPhone') }}</span>
        <span class="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded-full bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300">ON</span>
      </div>
      <div class="flex items-center justify-between py-2 px-3 rounded-md bg-gray-50 dark:bg-gray-800">
        <span class="text-sm text-gray-700 dark:text-gray-300">{{ t('review.workbench.rules.regexIds') }}</span>
        <span class="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded-full bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300">ON</span>
      </div>
    </div>

    <!-- Review policy -->
    <div class="border-t border-gray-200 dark:border-gray-700 pt-3 space-y-2">
      <h4 class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
        {{ t('review.workbench.rules.reviewPolicy') }}
      </h4>
      <ul class="text-xs text-gray-600 dark:text-gray-400 space-y-1 list-disc list-inside">
        <li>{{ t('review.workbench.rules.autoAcceptLow') }}</li>
        <li>{{ t('review.workbench.rules.requireReviewHigh') }}</li>
      </ul>
    </div>
  </div>
</template>
