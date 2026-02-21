<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="space-y-3 text-sm text-gray-700 dark:text-gray-300">
    <h4 class="font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.training.piiTypes.heading') }}
    </h4>
    <p>{{ t('review.training.piiTypes.text') }}</p>
    <div class="space-y-2">
      <div class="flex items-center gap-2">
        <span class="inline-block w-4 h-4 rounded bg-green-200/60 border border-green-400" />
        <span>{{ t('review.training.piiTypes.high') }}</span>
      </div>
      <div class="flex items-center gap-2">
        <span class="inline-block w-4 h-4 rounded bg-yellow-200/60 border border-yellow-400" />
        <span>{{ t('review.training.piiTypes.medium') }}</span>
      </div>
      <div class="flex items-center gap-2">
        <span class="inline-block w-4 h-4 rounded bg-orange-200/60 border border-orange-400" />
        <span>{{ t('review.training.piiTypes.low') }}</span>
      </div>
    </div>
  </div>
</template>
