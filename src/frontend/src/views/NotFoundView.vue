<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-100 dark:bg-gray-900">
    <div class="text-center">
      <h1 class="text-6xl font-bold text-gray-300 dark:text-gray-600">{{ t('common.notFound.title') }}</h1>
      <p class="mt-4 text-lg text-gray-600 dark:text-gray-400">{{ t('common.notFound.message') }}</p>
      <RouterLink
        to="/dashboard"
        class="mt-6 inline-block text-blue-600 hover:text-blue-500 dark:text-blue-400"
      >
        {{ t('common.notFound.backToDashboard') }}
      </RouterLink>
    </div>
  </div>
</template>
