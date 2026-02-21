<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../../stores/review'
import { ALL_ENTITY_TYPE_KEYS } from '../../constants/entityTypes'
import { generateFakeValue } from '../../utils/fakerGenerator'
import AppButton from '../ui/AppButton.vue'
import AppDropdown from '../ui/AppDropdown.vue'

const props = defineProps<{
  textSelection?: ReturnType<typeof import('../../composables/useTextSelection').useTextSelection>
}>()

const { t } = useI18n()
const reviewStore = useReviewStore()

const isExpanded = ref(false)
const searchText = ref('')
const entityType = ref('')
const replacementText = ref('')

watch(
  () => props.textSelection?.hasSelection.value,
  (hasSelection) => {
    if (hasSelection && props.textSelection) {
      isExpanded.value = true
      searchText.value = props.textSelection.selectedText.value
      window.getSelection()?.removeAllRanges()
      props.textSelection.clearSelection()
    }
  },
)

// Auto-fill replacement text when entity type changes
watch(entityType, (newType) => {
  if (newType) {
    replacementText.value = generateFakeValue(newType)
  }
})

const entityTypeOptions = ALL_ENTITY_TYPE_KEYS.map((key) => ({
  value: key,
  label: t(`review.entityTypes.${key}`, key),
}))

interface Occurrence {
  segmentId: string
  startOffset: number
  endOffset: number
  text: string
  overlaps: boolean
}

const occurrences = computed<Occurrence[]>(() => {
  const query = searchText.value.trim()
  if (!query) return []

  const results: Occurrence[] = []
  for (const segment of reviewStore.segments) {
    const text = segment.textContent
    let searchFrom = 0
    while (true) {
      const idx = text.indexOf(query, searchFrom)
      if (idx === -1) break
      const endIdx = idx + query.length

      const overlaps = reviewStore.entities.some(
        (e) =>
          e.segmentId === segment.id &&
          e.startOffset < endIdx &&
          e.endOffset > idx,
      )

      results.push({
        segmentId: segment.id,
        startOffset: idx,
        endOffset: endIdx,
        text: text.slice(idx, endIdx),
        overlaps,
      })
      searchFrom = idx + 1
    }
  }
  return results
})

const totalCount = computed(() => occurrences.value.length)
const availableCount = computed(() => occurrences.value.filter((o) => !o.overlaps).length)

function handleClose() {
  searchText.value = ''
  entityType.value = ''
  replacementText.value = ''
  isExpanded.value = false
}

async function handleAddAll() {
  if (availableCount.value === 0 || !replacementText.value.trim()) return

  const matches = occurrences.value
    .filter((o) => !o.overlaps)
    .map((o) => ({
      segmentId: o.segmentId,
      text: o.text,
      startOffset: o.startOffset,
      endOffset: o.endOffset,
    }))

  await reviewStore.addSearchMatchesAsEntities(
    matches,
    entityType.value || 'CUSTOM',
    replacementText.value.trim(),
  )

  handleClose()
}
</script>

<template>
  <div>
    <AppButton
      v-if="!isExpanded"
      size="sm"
      variant="secondary"
      class="w-full"
      @click="isExpanded = true"
    >
      {{ t('review.workbench.addByText.button') }}
    </AppButton>

    <div v-else class="rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 p-3 space-y-3">
      <div class="flex items-center justify-between">
        <span class="text-xs font-medium text-gray-700 dark:text-gray-300">
          {{ t('review.workbench.addByText.button') }}
        </span>
        <button
          class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
          @click="handleClose"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
        </button>
      </div>

      <!-- Two-column inputs: Klartext + Verfremdet -->
      <div class="grid grid-cols-2 gap-2">
        <div>
          <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">
            {{ t('review.workbench.addByText.klartextLabel') }}
          </label>
          <input
            v-model="searchText"
            :placeholder="t('review.workbench.addByText.placeholder')"
            class="w-full text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-2 py-1.5 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div>
          <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">
            {{ t('review.workbench.addByText.verfremdetLabel') }}
          </label>
          <input
            v-model="replacementText"
            :placeholder="t('review.workbench.addByText.verfremdetPlaceholder')"
            class="w-full text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-2 py-1.5 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      <div v-if="searchText.trim() && totalCount > 0" class="space-y-2">
        <p class="text-xs text-gray-500 dark:text-gray-400">
          {{ t('review.workbench.addByText.matchInfo', { total: totalCount, available: availableCount }) }}
        </p>

        <div>
          <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">
            {{ t('review.workbench.addByText.autoVerfremden') }}
          </label>
          <AppDropdown
            v-model="entityType"
            :options="entityTypeOptions"
            :placeholder="t('review.popover.selectType')"
          />
        </div>

        <AppButton
          size="sm"
          variant="primary"
          class="w-full"
          :disabled="availableCount === 0 || !replacementText.trim() || reviewStore.saving"
          @click="handleAddAll"
        >
          {{ t('review.workbench.addByText.addAll', { count: availableCount }) }}
        </AppButton>
      </div>

      <p v-else-if="searchText.trim() && totalCount === 0" class="text-xs text-gray-500 dark:text-gray-400">
        {{ t('review.workbench.search.noMatches') }}
      </p>
    </div>
  </div>
</template>
