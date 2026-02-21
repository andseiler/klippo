<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="space-y-3 text-sm text-gray-700 dark:text-gray-300">
    <h4 class="font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.training.manualAdd.heading') }}
    </h4>
    <p>{{ t('review.training.manualAdd.text1') }}</p>
    <ol class="list-decimal pl-5 space-y-1">
      <li>{{ t('review.training.manualAdd.step1') }}</li>
      <li>{{ t('review.training.manualAdd.step2') }}</li>
      <li>{{ t('review.training.manualAdd.step3') }}</li>
    </ol>
  </div>
</template>
