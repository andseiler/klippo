<script setup lang="ts">
import { ref, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import type { useDocumentSearch } from '../../composables/useDocumentSearch'
import { ALL_ENTITY_TYPE_KEYS } from '../../constants/entityTypes'
import { useReviewStore } from '../../stores/review'
import AppButton from '../ui/AppButton.vue'
import AppDropdown from '../ui/AppDropdown.vue'

const props = defineProps<{
  documentSearch: ReturnType<typeof useDocumentSearch>
}>()

const emit = defineEmits<{
  'add-all': [entityType: string]
}>()

const { t } = useI18n()
const reviewStore = useReviewStore()
const inputRef = ref<HTMLInputElement | null>(null)
const addEntityType = ref('')

const entityTypeOptions = ALL_ENTITY_TYPE_KEYS.map((key) => ({
  value: key,
  label: t(`review.entityTypes.${key}`, key),
}))

watch(
  () => props.documentSearch.isSearchOpen.value,
  (open) => {
    if (open) {
      nextTick(() => inputRef.value?.focus())
    }
  },
)

// Reset match index when query changes
watch(
  () => props.documentSearch.searchQuery.value,
  () => {
    props.documentSearch.currentMatchIndex.value = 0
  },
)

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    props.documentSearch.closeSearch()
  } else if (e.key === 'Enter') {
    if (e.shiftKey) {
      props.documentSearch.prevMatch()
    } else {
      props.documentSearch.nextMatch()
    }
  }
}

function handleGlobalKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
    e.preventDefault()
    props.documentSearch.toggleSearch()
  }
}

function handleAddAll() {
  if (!addEntityType.value) return
  emit('add-all', addEntityType.value)
  addEntityType.value = ''
}

onMounted(() => {
  document.addEventListener('keydown', handleGlobalKeydown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleGlobalKeydown)
})
</script>

<template>
  <div
    v-if="documentSearch.isSearchOpen.value"
    class="flex flex-col gap-2 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-yellow-50 dark:bg-yellow-900/10"
  >
    <div class="flex items-center gap-2">
      <input
        ref="inputRef"
        v-model="documentSearch.searchQuery.value"
        :placeholder="t('review.workbench.search.placeholder')"
        class="flex-1 text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-2 py-1 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
        @keydown="handleKeydown"
      />
      <span v-if="documentSearch.allMatches.value.length > 0" class="text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">
        {{ documentSearch.currentMatchIndex.value + 1 }} / {{ documentSearch.allMatches.value.length }}
      </span>
      <span v-else-if="documentSearch.searchQuery.value.trim()" class="text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">
        {{ t('review.workbench.search.noMatches') }}
      </span>
      <button
        class="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
        :title="t('review.workbench.search.prev')"
        :disabled="documentSearch.allMatches.value.length === 0"
        @click="documentSearch.prevMatch()"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7" /></svg>
      </button>
      <button
        class="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
        :title="t('review.workbench.search.next')"
        :disabled="documentSearch.allMatches.value.length === 0"
        @click="documentSearch.nextMatch()"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" /></svg>
      </button>
      <button
        class="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
        :title="t('review.workbench.search.close')"
        @click="documentSearch.closeSearch()"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
      </button>
    </div>
    <!-- Add all as PII row -->
    <div v-if="documentSearch.actionableMatches.value.length > 0" class="flex items-center gap-2">
      <span class="text-xs text-gray-500 dark:text-gray-400">
        {{ t('review.workbench.search.matchCount', { total: documentSearch.allMatches.value.length, available: documentSearch.actionableMatches.value.length }) }}
      </span>
      <div class="flex-1">
        <AppDropdown
          v-model="addEntityType"
          :options="entityTypeOptions"
          :placeholder="t('review.popover.selectType')"
        />
      </div>
      <AppButton
        size="sm"
        variant="primary"
        :disabled="!addEntityType || documentSearch.actionableMatches.value.length === 0 || reviewStore.saving"
        @click="handleAddAll"
      >
        {{ t('review.workbench.search.addAll') }}
      </AppButton>
    </div>
  </div>
</template>
