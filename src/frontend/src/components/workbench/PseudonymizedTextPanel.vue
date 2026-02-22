<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SegmentDto, EntityDto } from '../../api/types'
import type { useDocumentSearch } from '../../composables/useDocumentSearch'
import { useReviewStore } from '../../stores/review'
import SegmentRenderer from '../review/SegmentRenderer.vue'
import AppButton from '../ui/AppButton.vue'

const props = defineProps<{
  segments: SegmentDto[]
  entitiesBySegment: Map<string, EntityDto[]>
  documentSearch?: ReturnType<typeof useDocumentSearch>
  textSelection?: ReturnType<typeof import('../../composables/useTextSelection').useTextSelection>
}>()

const emit = defineEmits<{
  completeReview: []
}>()

const { t } = useI18n()
const reviewStore = useReviewStore()
const copied = ref(false)
const scrollContainerRef = ref<HTMLElement | null>(null)

defineExpose({ scrollContainer: scrollContainerRef })

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

function handleEntityReplacementUpdated(entityId: string, newText: string) {
  const entity = reviewStore.entities.find(e => e.id === entityId)
  if (!entity) return
  const oldToken = entity.replacementPreview || entity.text
  reviewStore.updateReplacementByToken(oldToken, newText)
}

async function copyToClipboard() {
  await navigator.clipboard.writeText(reviewStore.pseudonymizedFullText)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}

function handleActionClick() {
  if (reviewStore.reviewCompleted) {
    copyToClipboard()
  } else {
    emit('completeReview')
  }
}
</script>

<template>
  <div class="flex flex-col h-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 overflow-hidden">
    <!-- Toolbar -->
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex-shrink-0">
      <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
        {{ t('review.workbench.pseudoPanel.title') }}
      </span>
      <AppButton
        variant="success"
        size="sm"
        @click="handleActionClick"
      >
        <template v-if="reviewStore.reviewCompleted">
          {{ copied ? t('review.workbench.export.copied') : t('review.workbench.pseudoPanel.copyButton') }}
        </template>
        <template v-else>
          {{ t('review.workbench.header.completeReview') }}
        </template>
      </AppButton>
    </div>

    <!-- Scrollable pseudo text area -->
    <div
      ref="scrollContainerRef"
      :class="[
        'flex-1 overflow-y-auto p-6 lg:p-8',
        !reviewStore.reviewCompleted ? 'select-none' : ''
      ]"
      @mouseup="textSelection?.handleMouseUp($event, 'pseudonymized')"
    >
      <div class="max-w-3xl mx-auto space-y-4">
        <div
          v-for="segment in segments"
          :key="segment.id"
          class="leading-relaxed text-gray-900 dark:text-gray-100"
        >
          <SegmentRenderer
            :segment="segment"
            :entities="entitiesBySegment.get(segment.id) || []"
            :editable="true"
            display-mode="pseudonymized"
            :search-matches="documentSearch?.matchesBySegment.value.get(segment.id) || []"
            :active-match-index="documentSearch?.currentMatchIndex.value ?? -1"
            :global-match-offset="getGlobalMatchOffset(segment.id)"
            @entity-replacement-updated="handleEntityReplacementUpdated"
          />
        </div>
      </div>
    </div>
  </div>
</template>
