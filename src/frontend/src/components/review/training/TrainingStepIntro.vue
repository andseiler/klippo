<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="space-y-3 text-sm text-gray-700 dark:text-gray-300">
    <h4 class="font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.training.intro.heading') }}
    </h4>
    <p>{{ t('review.training.intro.text1') }}</p>
    <p>{{ t('review.training.intro.text2') }}</p>
  </div>
</template>
