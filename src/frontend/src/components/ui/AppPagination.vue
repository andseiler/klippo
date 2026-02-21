<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

interface Props {
  page: number
  totalPages: number
  totalCount: number
  pageSize: number
}

const props = defineProps<Props>()
const emit = defineEmits<{
  'update:page': [value: number]
}>()

const { t } = useI18n()

const startEntry = computed(() => (props.page - 1) * props.pageSize + 1)
const endEntry = computed(() => Math.min(props.page * props.pageSize, props.totalCount))

function goToPage(p: number) {
  if (p >= 1 && p <= props.totalPages) {
    emit('update:page', p)
  }
}

const visiblePages = computed(() => {
  const pages: number[] = []
  const start = Math.max(1, props.page - 2)
  const end = Math.min(props.totalPages, props.page + 2)
  for (let i = start; i <= end; i++) {
    pages.push(i)
  }
  return pages
})
</script>

<template>
  <div class="flex flex-col sm:flex-row items-center justify-between gap-4 py-3">
    <div class="text-sm text-gray-600 dark:text-gray-400">
      {{ t('common.showing') }} {{ startEntry }}–{{ endEntry }} {{ t('common.of') }} {{ totalCount }} {{ t('common.entries') }}
    </div>
    <div class="flex items-center gap-1">
      <button
        :disabled="page <= 1"
        class="rounded-md px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed text-gray-700 dark:text-gray-300"
        @click="goToPage(page - 1)"
      >
        &laquo;
      </button>
      <button
        v-for="p in visiblePages"
        :key="p"
        :class="[
          'rounded-md px-3 py-1.5 text-sm border',
          p === page
            ? 'bg-blue-600 text-white border-blue-600'
            : 'border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700',
        ]"
        @click="goToPage(p)"
      >
        {{ p }}
      </button>
      <button
        :disabled="page >= totalPages"
        class="rounded-md px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed text-gray-700 dark:text-gray-300"
        @click="goToPage(page + 1)"
      >
        &raquo;
      </button>
    </div>
  </div>
</template>
