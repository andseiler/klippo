<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import AppAlert from '../../ui/AppAlert.vue'

const { t } = useI18n()
</script>

<template>
  <div class="space-y-3 text-sm text-gray-700 dark:text-gray-300">
    <h4 class="font-medium text-gray-900 dark:text-gray-100">
      {{ t('review.training.automationBias.heading') }}
    </h4>
    <AppAlert variant="warning">
      {{ t('review.training.automationBias.warning') }}
    </AppAlert>
    <p>{{ t('review.training.automationBias.text1') }}</p>
    <p>{{ t('review.training.automationBias.text2') }}</p>
  </div>
</template>
