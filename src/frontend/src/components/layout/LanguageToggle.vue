<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { locale } = useI18n()

function toggleLocale() {
  const next = locale.value === 'de' ? 'en' : 'de'
  locale.value = next
  localStorage.setItem('locale', next)
  document.documentElement.lang = next
}
</script>

<template>
  <button
    class="rounded-md px-2 py-1 text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
    @click="toggleLocale"
  >
    {{ locale === 'de' ? 'EN' : 'DE' }}
  </button>
</template>
