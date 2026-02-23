<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SegmentDto, EntityDto, EntityOffsetUpdate } from '../../api/types'
import type { ViewMode } from '../../stores/review'
import type { useDocumentSearch } from '../../composables/useDocumentSearch'
import { useReviewStore } from '../../stores/review'
import { getEntityTypeHighlightClass } from '../../constants/entityTypeColors'
import SegmentRenderer from '../review/SegmentRenderer.vue'
import DocumentSearchBar from './DocumentSearchBar.vue'

const props = defineProps<{
  segments: SegmentDto[]
  entitiesBySegment: Map<string, EntityDto[]>
  displayMode: ViewMode
  textSelection: ReturnType<typeof import('../../composables/useTextSelection').useTextSelection>
  documentSearch?: ReturnType<typeof useDocumentSearch>
}>()

const emit = defineEmits<{
  'add-search-matches': [entityType: string]
}>()

const { t } = useI18n()
const reviewStore = useReviewStore()
const containerRef = ref<HTMLElement | null>(null)

const isEditable = computed(() => {
  const status = reviewStore.status
  return status === 'inreview' || status === 'readyreview' || status === 'pseudonymized'
})

// Compute global match offset per segment (for active match highlighting)
function getGlobalMatchOffset(segmentId: string): number {
  if (!props.documentSearch) return 0
  let offset = 0
  for (const segment of props.segments) {
    if (segment.id === segmentId) return offset
    const matches = props.documentSearch.matchesBySegment.value.get(segment.id)
    offset += matches?.length ?? 0
  }
  return offset
}

// Auto-scroll to active search match
watch(
  () => props.documentSearch?.currentMatch?.value,
  () => {
    nextTick(() => {
      if (!containerRef.value) return
      const activeMark = containerRef.value.querySelector('mark[data-search-match].bg-orange-300, mark[data-search-match].dark\\:bg-orange-600')
        || containerRef.value.querySelector('mark.bg-orange-300')
      if (activeMark) {
        activeMark.scrollIntoView({ behavior: 'smooth', block: 'center' })
      }
    })
  },
)

function handleSegmentUpdated(segmentId: string, newText: string, entityOffsets: EntityOffsetUpdate[]) {
  reviewStore.updateSegmentText(segmentId, newText, entityOffsets)
}

const toolbarTitle = computed(() => {
  if (props.displayMode === 'depseudonymized') {
    return t('review.workbench.modeToggle.klartext')
  }
  return t('review.workbench.originalPanel.title')
})

defineExpose({ scrollContainer: containerRef })
</script>

<template>
  <div class="flex flex-col h-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 overflow-hidden">
    <!-- Sticky toolbar -->
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex-shrink-0">
      <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
        {{ toolbarTitle }}
      </span>
    </div>

    <!-- Search bar -->
    <DocumentSearchBar
      v-if="documentSearch && displayMode === 'pseudonymized'"
      :document-search="documentSearch"
      @add-all="emit('add-search-matches', $event)"
    />

    <!-- Scrollable document area -->
    <div
      ref="containerRef"
      class="flex-1 overflow-y-auto min-h-0 p-6 lg:p-8"
      @mouseup="displayMode === 'pseudonymized' ? textSelection.handleMouseUp($event, displayMode) : undefined"
    >
      <!-- De-pseudonymized: paste AI output + live-replace -->
      <template v-if="displayMode === 'depseudonymized'">
        <div class="max-w-3xl mx-auto space-y-6">
          <!-- Input textarea -->
          <div>
            <label class="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">
              {{ t('review.workbench.depseudo.inputLabel') }}
            </label>
            <textarea
              v-model="reviewStore.depseudoInputText"
              :placeholder="t('review.workbench.depseudo.inputPlaceholder')"
              class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 p-4 text-sm text-gray-900 dark:text-gray-100 leading-relaxed resize-y min-h-[120px] focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>

          <!-- Rendered output -->
          <div v-if="reviewStore.depseudoInputText">
            <div class="flex items-center gap-2 mb-1">
              <label class="block text-xs font-medium text-gray-500 dark:text-gray-400">
                {{ t('review.workbench.depseudo.outputLabel') }}
              </label>
              <span
                v-if="reviewStore.depseudoStats.replacedCount > 0"
                class="inline-flex items-center rounded-full bg-green-100 dark:bg-green-900/30 px-2 py-0.5 text-xs font-medium text-green-700 dark:text-green-400"
              >
                {{ t('review.workbench.depseudo.replacedCount', { count: reviewStore.depseudoStats.replacedCount }) }}
              </span>
            </div>
            <div class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800 p-4 text-sm text-gray-900 dark:text-gray-100 whitespace-pre-wrap leading-relaxed">
              <template v-for="(fragment, idx) in reviewStore.depseudoOutputFragments" :key="idx">
                <span v-if="fragment.type === 'text'">{{ fragment.content }}</span>
                <mark
                  v-else
                  :class="['rounded px-0.5', getEntityTypeHighlightClass(fragment.entityType)]"
                  :title="fragment.token"
                >{{ fragment.content }}</mark>
              </template>
            </div>
          </div>
        </div>
      </template>

      <!-- Verfremden mode: show original text with entity highlights -->
      <template v-else>
        <div class="max-w-3xl mx-auto space-y-4">
          <div
            v-for="segment in segments"
            :key="segment.id"
            class="leading-relaxed text-gray-900 dark:text-gray-100"
          >
            <SegmentRenderer
              :segment="segment"
              :entities="entitiesBySegment.get(segment.id) || []"
              :editable="isEditable"
              display-mode="original"
              :search-matches="documentSearch?.matchesBySegment.value.get(segment.id) || []"
              :active-match-index="documentSearch?.currentMatchIndex.value ?? -1"
              :global-match-offset="getGlobalMatchOffset(segment.id)"
              @segment-updated="handleSegmentUpdated"
            />
          </div>
        </div>
      </template>
    </div>
  </div>
</template>
