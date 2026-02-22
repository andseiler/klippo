<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch, type ComponentPublicInstance } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../stores/review'
import { useJobsStore } from '../stores/jobs'
import { updateJob } from '../api/jobs'
import { getHealthInfo } from '../api/health'
import { useReviewNavigation } from '../composables/useReviewNavigation'
import { useTextSelection } from '../composables/useTextSelection'
import { useDocumentSearch } from '../composables/useDocumentSearch'
import { useReviewerTraining } from '../composables/useReviewerTraining'
import { useScrollSync } from '../composables/useScrollSync'
import type { ViewMode } from '../stores/review'
import WorkbenchHeader from '../components/workbench/WorkbenchHeader.vue'
import WorkbenchDocumentViewer from '../components/workbench/WorkbenchDocumentViewer.vue'
import WorkbenchRightRail from '../components/workbench/WorkbenchRightRail.vue'
import PseudonymizedTextPanel from '../components/workbench/PseudonymizedTextPanel.vue'
import ReviewerTrainingModal from '../components/review/ReviewerTrainingModal.vue'
import CompleteReviewModal from '../components/review/CompleteReviewModal.vue'
import LlmScanModal from '../components/workbench/LlmScanModal.vue'
import AppSpinner from '../components/ui/AppSpinner.vue'
import AppAlert from '../components/ui/AppAlert.vue'
import AppModal from '../components/ui/AppModal.vue'
import AppButton from '../components/ui/AppButton.vue'

const props = defineProps<{
  id: string
}>()

const { t } = useI18n()
const reviewStore = useReviewStore()
const jobsStore = useJobsStore()

const { setupKeyboardNav, teardownKeyboardNav } = useReviewNavigation()
const textSelection = useTextSelection()
const { segments: _segments, entities: _entities } = storeToRefs(reviewStore)
const documentSearch = useDocumentSearch(_segments, _entities)
const training = useReviewerTraining()
const { scrollElA, scrollElB } = useScrollSync()

const docViewerRef = ref<ComponentPublicInstance | null>(null)
const pseudoPanelRef = ref<ComponentPublicInstance | null>(null)

watch(() => (docViewerRef.value as any)?.scrollContainer, (el: HTMLElement | null) => {
  scrollElA.value = el ?? null
}, { immediate: true })

watch(() => (pseudoPanelRef.value as any)?.scrollContainer, (el: HTMLElement | null) => {
  scrollElB.value = el ?? null
}, { immediate: true })

const showCompleteReviewModal = ref(false)
const showLlmScanModal = ref(false)
const showRescanModal = ref(false)
const llmAvailable = ref(true)
const showLlmNotConfiguredModal = ref(false)

onMounted(async () => {
  await Promise.all([
    reviewStore.fetchReviewData(props.id),
    jobsStore.fetchJob(props.id),
  ])
  // Populate fileName from job data
  if (jobsStore.currentJob) {
    reviewStore.fileName = jobsStore.currentJob.fileName
  }
  setupKeyboardNav()
  training.checkTrainingNeeded()

  getHealthInfo()
    .then((info) => { llmAvailable.value = info.llmAvailable })
    .catch(() => { /* health check failed, assume unavailable */ })
})

onUnmounted(() => {
  teardownKeyboardNav()
  reviewStore.resetReviewState()
})

// Keep fileName in sync if job data updates
watch(
  () => jobsStore.currentJob?.fileName,
  (name) => {
    if (name) reviewStore.fileName = name
  },
)

async function handleEndReview() {
  showCompleteReviewModal.value = false
  await reviewStore.submitCompleteReview(props.id)
}

async function handleUpdateFileName(newName: string) {
  try {
    const updated = await updateJob(props.id, { fileName: newName })
    reviewStore.fileName = updated.fileName
    if (jobsStore.currentJob) {
      jobsStore.currentJob.fileName = updated.fileName
    }
  } catch {
    // Revert on failure - fileName stays as-is since we didn't change it yet
  }
}

function handleLlmScan() {
  if (!llmAvailable.value) {
    showLlmNotConfiguredModal.value = true
    return
  }
  showLlmScanModal.value = true
}

function handleDisplayModeUpdate(mode: ViewMode) {
  reviewStore.setViewMode(mode)
}
</script>

<template>
  <div class="flex flex-col h-screen bg-slate-50 dark:bg-gray-950">
    <!-- Loading -->
    <div v-if="reviewStore.loading" class="flex items-center justify-center h-screen">
      <AppSpinner />
    </div>

    <!-- Error -->
    <div v-else-if="reviewStore.error" class="flex items-center justify-center h-screen p-8">
      <AppAlert variant="error" class="max-w-md">
        {{ reviewStore.error }}
      </AppAlert>
    </div>

    <!-- Workbench UI -->
    <template v-else>
      <!-- Zone 1: Header -->
      <WorkbenchHeader
        :job-id="id"
        :file-name="reviewStore.fileName || id"
        :status="reviewStore.status || (jobsStore.currentJob?.status ?? '')"
        :saving="reviewStore.saving || reviewStore.completeReviewLoading"
        :display-mode="reviewStore.viewMode"
        @llm-scan="handleLlmScan"
        @rescan="showRescanModal = true"
        @update:file-name="handleUpdateFileName"
        @update:display-mode="handleDisplayModeUpdate"
      />

      <!-- Zone 2 + 3: Document viewer + Right rail -->
      <main class="flex-1 overflow-hidden mx-auto max-w-[1800px] w-full px-4 py-4">
        <div class="grid grid-cols-1 gap-4 lg:grid-cols-12 h-full">
          <!-- Verfremden mode: 3-column layout -->
          <template v-if="reviewStore.viewMode === 'pseudonymized'">
            <WorkbenchDocumentViewer
              ref="docViewerRef"
              class="lg:col-span-4"
              :segments="reviewStore.segments"
              :entities-by-segment="reviewStore.entitiesBySegment"
              :display-mode="reviewStore.viewMode"
              :text-selection="textSelection"
              :document-search="documentSearch"
              @add-search-matches="(type: string) => reviewStore.addSearchMatchesAsEntities(documentSearch.actionableMatches.value, type)"
            />
            <PseudonymizedTextPanel
              ref="pseudoPanelRef"
              class="lg:col-span-4"
              :segments="reviewStore.segments"
              :entities-by-segment="reviewStore.entitiesBySegment"
              :document-search="documentSearch"
              :text-selection="textSelection"
              @complete-review="showCompleteReviewModal = true"
            />
            <WorkbenchRightRail class="lg:col-span-4" :text-selection="textSelection" />
          </template>

          <!-- Klartext mode: 2-column layout -->
          <template v-else>
            <WorkbenchDocumentViewer
              class="lg:col-span-8"
              :segments="reviewStore.segments"
              :entities-by-segment="reviewStore.entitiesBySegment"
              :display-mode="reviewStore.viewMode"
              :text-selection="textSelection"
            />
            <WorkbenchRightRail class="lg:col-span-4" :text-selection="textSelection" />
          </template>
        </div>
      </main>


      <ReviewerTrainingModal
        v-if="training.isTrainingModalOpen.value"
        :training="training"
      />

      <CompleteReviewModal
        v-model:open="showCompleteReviewModal"
        @continue="showCompleteReviewModal = false"
        @end="handleEndReview"
      />

      <LlmScanModal
        v-model:open="showLlmScanModal"
        :job-id="id"
      />

      <LlmScanModal
        v-model:open="showRescanModal"
        :job-id="id"
        mode="rescan"
      />

      <AppModal
        :open="showLlmNotConfiguredModal"
        :title="t('review.workbench.llmScan.notConfiguredTitle')"
        @update:open="showLlmNotConfiguredModal = $event"
      >
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ t('review.workbench.llmScan.notConfiguredMessage') }}
        </p>
        <div class="flex justify-end pt-4">
          <AppButton variant="secondary" @click="showLlmNotConfiguredModal = false">
            {{ t('review.workbench.llmScan.close') }}
          </AppButton>
        </div>
      </AppModal>
    </template>
  </div>
</template>
