<script setup lang="ts">
import { ref, computed, onUnmounted, watch, type ComponentPublicInstance } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useFileUpload } from '../composables/useFileUpload'
import { useEstimatedProgress } from '../composables/useEstimatedProgress'
import { useTextSelection } from '../composables/useTextSelection'
import { useDocumentSearch } from '../composables/useDocumentSearch'
import { useScrollSync } from '../composables/useScrollSync'
import { useCollapsiblePanels } from '../composables/useCollapsiblePanels'
import { useJobsStore } from '../stores/jobs'
import { useReviewStore } from '../stores/review'
import type { ViewMode } from '../stores/review'
import { guestAuth } from '../api/auth'
import { getJob, cancelJob } from '../api/jobs'
import { getHealthInfo } from '../api/health'
import FileDropZone from '../components/upload/FileDropZone.vue'
import WorkbenchHeader from '../components/workbench/WorkbenchHeader.vue'
import WorkbenchDocumentViewer from '../components/workbench/WorkbenchDocumentViewer.vue'
import WorkbenchRightRail from '../components/workbench/WorkbenchRightRail.vue'
import PseudonymizedTextPanel from '../components/workbench/PseudonymizedTextPanel.vue'
import CompleteReviewModal from '../components/review/CompleteReviewModal.vue'
import LlmScanModal from '../components/workbench/LlmScanModal.vue'
import AppButton from '../components/ui/AppButton.vue'
import AppAlert from '../components/ui/AppAlert.vue'
import AppModal from '../components/ui/AppModal.vue'
import AppSpinner from '../components/ui/AppSpinner.vue'
import AppProgress from '../components/ui/AppProgress.vue'
import LanguageToggle from '../components/layout/LanguageToggle.vue'
import PlaygroundFooter from '../components/playground/PlaygroundFooter.vue'

const { t } = useI18n()
const jobsStore = useJobsStore()
const reviewStore = useReviewStore()
const textSelection = useTextSelection()
const estimatedProgress = useEstimatedProgress()
const { segments: _segments, entities: _entities } = storeToRefs(reviewStore)
const documentSearch = useDocumentSearch(_segments, _entities)
const { scrollElA, scrollElB } = useScrollSync()
useCollapsiblePanels()

const docViewerRef = ref<ComponentPublicInstance | null>(null)
const pseudoPanelRef = ref<ComponentPublicInstance | null>(null)

watch(() => (docViewerRef.value as any)?.scrollContainer, (el: HTMLElement | null) => {
  scrollElA.value = el ?? null
}, { immediate: true })

watch(() => (pseudoPanelRef.value as any)?.scrollContainer, (el: HTMLElement | null) => {
  scrollElB.value = el ?? null
}, { immediate: true })

// Phase: 'upload' | 'processing' | 'workbench'
const phase = ref<'upload' | 'processing' | 'workbench'>('upload')
const error = ref('')
const jobId = ref('')
let pollTimer: ReturnType<typeof setInterval> | null = null

// Upload state
const { file, validationError, selectFile, clearFile, formattedSize } = useFileUpload()

const activeTab = ref<'file' | 'text'>('file')
const pastedText = ref('')
const textDocName = ref('playground-doc')
const submitting = ref(false)

const hasTextContent = computed(() => pastedText.value.trim().length > 0)
const hasInput = computed(() =>
  activeTab.value === 'file' ? !!file.value : hasTextContent.value
)

// Workbench state
const showCompleteReviewModal = ref(false)
const showLlmScanModal = ref(false)
const showRescanModal = ref(false)
const llmAvailable = ref(true)
const showLlmNotConfiguredModal = ref(false)
const cancelling = ref(false)

async function ensureGuestToken() {
  try {
    const auth = await guestAuth()
    // Only swap tokens AFTER successful guestAuth
    const existingToken = localStorage.getItem('accessToken')
    if (existingToken && localStorage.getItem('isGuest') !== 'true') {
      localStorage.setItem('previousToken', existingToken)
    }
    localStorage.setItem('accessToken', auth.accessToken)
    localStorage.setItem('isGuest', 'true')
  } catch (e: unknown) {
    if (e && typeof e === 'object' && 'response' in e) {
      const axiosError = e as { response?: { status?: number } }
      if (axiosError.response?.status === 429) {
        error.value = t('playground.dailyLimitReached')
        throw new Error('Daily limit reached')
      }
      if (axiosError.response?.status === 503) {
        error.value = t('playground.serviceUnavailable')
        throw new Error('Service unavailable')
      }
    }
    error.value = t('playground.guestError')
    throw new Error('Guest auth failed')
  }
}

async function handleSubmit() {
  let submitFile: File

  if (activeTab.value === 'text') {
    const text = pastedText.value.trim()
    if (!text) return
    const blob = new Blob([text], { type: 'text/plain' })
    submitFile = new File([blob], textDocName.value + '.txt', { type: 'text/plain' })
  } else {
    if (!file.value) return
    submitFile = file.value
  }

  submitting.value = true
  error.value = ''

  // Compute word count for progress estimation
  let wordCount: number | undefined
  if (activeTab.value === 'text') {
    wordCount = pastedText.value.trim().split(/\s+/).length
  } else if (file.value) {
    // Estimate: ~5 chars per word for typical text
    wordCount = Math.round(file.value.size / 5)
  }

  try {
    await ensureGuestToken()

    const job = await jobsStore.submitJob({
      file: submitFile,
    })
    jobId.value = job.id
    phase.value = 'processing'
    estimatedProgress.start(wordCount)
    startPolling()
  } catch (e) {
    if (!error.value) {
      if (e && typeof e === 'object' && 'response' in e) {
        const axiosError = e as { response?: { data?: { message?: string }, status?: number } }
        error.value = axiosError.response?.data?.message || t('playground.guestError')
      } else {
        error.value = e instanceof Error ? e.message : t('playground.guestError')
      }
    }
  } finally {
    submitting.value = false
  }
}

function startPolling() {
  pollTimer = setInterval(async () => {
    try {
      const job = await getJob(jobId.value)
      if (job.status === 'readyreview' || job.status === 'inreview' ||
          job.status === 'pseudonymized' || job.status === 'scanpassed' ||
          job.status === 'scanfailed' || job.status === 'depseudonymized') {
        stopPolling()
        estimatedProgress.complete()
        await enterWorkbench()
      } else if (job.status === 'failed') {
        stopPolling()
        estimatedProgress.reset()
        error.value = t('playground.failed')
        phase.value = 'upload'
      } else if (job.status === 'cancelled') {
        stopPolling()
        estimatedProgress.reset()
        error.value = t('playground.cancelled')
        phase.value = 'upload'
      }
    } catch {
      // Keep polling
    }
  }, 2000)
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

async function enterWorkbench() {
  await Promise.all([
    reviewStore.fetchReviewData(jobId.value),
    jobsStore.fetchJob(jobId.value),
  ])
  if (jobsStore.currentJob) {
    reviewStore.fileName = jobsStore.currentJob.fileName
  }
  phase.value = 'workbench'

  getHealthInfo()
    .then((info) => { llmAvailable.value = info.llmAvailable })
    .catch(() => { /* health check failed, assume unavailable */ })
}

function restoreToken() {
  const prev = localStorage.getItem('previousToken')
  if (prev) {
    localStorage.setItem('accessToken', prev)
  } else {
    localStorage.removeItem('accessToken')
  }
  localStorage.removeItem('previousToken')
  localStorage.removeItem('isGuest')
}

async function handleCancel() {
  if (!jobId.value || cancelling.value) return
  cancelling.value = true
  try {
    await cancelJob(jobId.value)
  } catch {
    // Polling will pick up the status change
  } finally {
    cancelling.value = false
  }
}

function handleStartOver() {
  stopPolling()
  estimatedProgress.reset()
  reviewStore.resetReviewState()
  restoreToken()
  clearFile()
  pastedText.value = ''
  error.value = ''
  jobId.value = ''
  phase.value = 'upload'
}

function handleLlmScan() {
  if (!llmAvailable.value) {
    showLlmNotConfiguredModal.value = true
    return
  }
  showLlmScanModal.value = true
}

async function handleEndReview() {
  showCompleteReviewModal.value = false
  await reviewStore.submitCompleteReview(jobId.value)
}

function handleDisplayModeUpdate(mode: ViewMode) {
  reviewStore.setViewMode(mode)
}

async function handleUpdateFileName(newName: string) {
  const { updateJob } = await import('../api/jobs')
  try {
    const updated = await updateJob(jobId.value, { fileName: newName })
    reviewStore.fileName = updated.fileName
    if (jobsStore.currentJob) {
      jobsStore.currentJob.fileName = updated.fileName
    }
  } catch {
    // Revert on failure
  }
}

onUnmounted(() => {
  stopPolling()
  reviewStore.resetReviewState()
  if (localStorage.getItem('isGuest') === 'true') {
    restoreToken()
  }
})
</script>

<template>
  <div :class="['flex flex-col bg-slate-50 dark:bg-gray-950', phase !== 'upload' ? 'h-screen' : 'min-h-screen']">
    <!-- Phase 1: Upload (Landing Page) -->
    <template v-if="phase === 'upload'">
      <div class="max-w-5xl mx-auto w-full px-4 py-6 space-y-8">

        <!-- a) Header bar -->
        <header class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-white font-bold text-sm">K</div>
            <span class="font-bold text-gray-900 dark:text-gray-100">Klippo</span>
          </div>
          <div class="flex items-center gap-4">
            <a href="https://andreas-seiler.net" target="_blank" rel="noopener" class="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hidden sm:inline">andreas-seiler.net</a>
            <LanguageToggle />
          </div>
        </header>

        <!-- b) Hero -->
        <div>
          <h1 class="text-3xl font-bold text-gray-900 dark:text-gray-100 leading-tight">
            {{ t('playground.heroTitle') }}
          </h1>
          <p class="mt-3 text-base text-gray-600 dark:text-gray-400">
            {{ t('playground.heroDescription') }}
          </p>
        </div>

        <!-- c) How it works card -->
        <div class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6">
          <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">{{ t('playground.howItWorks.title') }}</h2>
          <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">{{ t('playground.howItWorks.description') }}</p>
        </div>

        <!-- d) Beta notice -->
        <AppAlert variant="info" class="text-sm">
          <p class="font-medium">{{ t('playground.betaNotice') }}</p>
          <p class="mt-1">
            {{ t('playground.betaAccount') }}
            <a href="mailto:mail@andreas-seiler.net" class="font-medium underline">
              mail@andreas-seiler.net
            </a>
          </p>
          <p class="mt-1 text-xs opacity-80">
            {{ t('playground.betaTechnical') }}
          </p>
        </AppAlert>

        <!-- e) Upload area -->
        <div class="space-y-4">
          <!-- Tab bar -->
          <div class="flex border-b border-gray-200 dark:border-gray-700">
            <button
              :class="[
                'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
                activeTab === 'file'
                  ? 'border-blue-500 text-blue-600 dark:text-blue-400'
                  : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300',
              ]"
              @click="activeTab = 'file'"
            >
              {{ t('playground.tabs.file') }}
            </button>
            <button
              :class="[
                'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
                activeTab === 'text'
                  ? 'border-blue-500 text-blue-600 dark:text-blue-400'
                  : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300',
              ]"
              @click="activeTab = 'text'"
            >
              {{ t('playground.tabs.text') }}
            </button>
          </div>

          <!-- File tab -->
          <div v-if="activeTab === 'file'">
            <div v-if="!file">
              <FileDropZone @file-selected="selectFile" />
              <AppAlert v-if="validationError" variant="error" class="mt-3">
                {{ t(`upload.${validationError}`) }}
              </AppAlert>
            </div>
            <div v-else class="flex items-center justify-between bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-4">
              <div>
                <p class="text-sm font-medium text-gray-900 dark:text-gray-100">{{ file.name }}</p>
                <p class="text-xs text-gray-500 dark:text-gray-400">{{ formattedSize }}</p>
              </div>
              <AppButton variant="ghost" size="sm" @click="clearFile">
                {{ t('common.reset') }}
              </AppButton>
            </div>
          </div>

          <!-- Text tab -->
          <div v-if="activeTab === 'text'" class="space-y-4">
            <textarea
              v-model="pastedText"
              :placeholder="t('upload.textInput.textPlaceholder')"
              class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 p-4 text-sm text-gray-900 dark:text-gray-100 leading-relaxed resize-y min-h-[200px] focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>

          <!-- Submit -->
          <div v-if="hasInput">
            <AppAlert v-if="error" variant="error" class="mb-4">{{ error }}</AppAlert>
            <AppButton
              :loading="submitting"
              :disabled="submitting"
              class="w-full"
              @click="handleSubmit"
            >
              {{ submitting ? t('playground.submitting') : t('playground.submit') }}
            </AppButton>
          </div>
        </div>

        <!-- f) About Andreas card -->
        <div class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6">
          <div class="flex flex-col sm:flex-row gap-6">
            <div class="flex-1">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">{{ t('playground.about.title') }}</h2>
              <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">{{ t('playground.about.description') }}</p>
              <div class="mt-4 flex flex-wrap gap-3">
                <a href="https://andreas-seiler.net" target="_blank" rel="noopener">
                  <AppButton variant="secondary" size="sm">{{ t('playground.about.viewProfile') }}</AppButton>
                </a>
                <a href="mailto:mail@andreas-seiler.net">
                  <AppButton variant="primary" size="sm">mail@andreas-seiler.net</AppButton>
                </a>
              </div>
            </div>
            <div class="flex-shrink-0 flex items-start justify-center sm:justify-end">
              <img
                src="https://andreas-seiler.net/profilfoto.jpg"
                alt="Andreas Seiler"
                class="w-24 h-24 rounded-full object-cover"
              />
            </div>
          </div>
        </div>

        <!-- g) Full access card -->
        <div class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6">
          <div class="flex flex-col sm:flex-row sm:items-center gap-4">
            <div class="flex-1">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">{{ t('playground.fullAccess.title') }}</h2>
              <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">{{ t('playground.fullAccess.description') }}</p>
            </div>
            <a href="mailto:mail@andreas-seiler.net" class="flex-shrink-0">
              <AppButton variant="primary">{{ t('playground.fullAccess.cta') }}</AppButton>
            </a>
          </div>
        </div>

      </div>
    </template>

    <!-- Phase 2: Processing -->
    <template v-if="phase === 'processing'">
      <div class="flex flex-col items-center justify-center h-screen gap-4">
        <AppSpinner />
        <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">
          {{ t('playground.processing.title') }}
        </h2>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {{ t('playground.processing.description') }}
        </p>
        <div class="w-64">
          <AppProgress :value="estimatedProgress.progress.value" />
        </div>
        <p class="text-xs text-gray-400 dark:text-gray-500">
          {{ t('playground.processing.elapsed', { seconds: estimatedProgress.elapsedSeconds.value }) }}
        </p>
        <AppButton
          variant="secondary"
          size="sm"
          :loading="cancelling"
          :disabled="cancelling"
          @click="handleCancel"
        >
          {{ cancelling ? t('playground.cancelling') : t('playground.cancel') }}
        </AppButton>
      </div>
    </template>

    <!-- Phase 3: Workbench -->
    <template v-if="phase === 'workbench'">
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
        <WorkbenchHeader
          :job-id="jobId"
          :file-name="reviewStore.fileName || jobId"
          :status="reviewStore.status || (jobsStore.currentJob?.status ?? '')"
          :saving="reviewStore.saving || reviewStore.completeReviewLoading"
          :display-mode="reviewStore.viewMode"
          back-to="/playground"
          playground
          @back="handleStartOver"
          @llm-scan="handleLlmScan"
          @rescan="showRescanModal = true"
          @update:file-name="handleUpdateFileName"
          @update:display-mode="handleDisplayModeUpdate"
        />

        <main class="flex-1 overflow-hidden mx-auto w-full px-4 py-4">
          <div class="grid grid-cols-1 gap-4 lg:grid-cols-12 h-full">
            <!-- Verfremden mode: 3-column layout -->
            <template v-if="reviewStore.viewMode === 'pseudonymized'">
              <div class="lg:col-span-4">
                <WorkbenchDocumentViewer
                  ref="docViewerRef"
                  :segments="reviewStore.segments"
                  :entities-by-segment="reviewStore.entitiesBySegment"
                  :display-mode="reviewStore.viewMode"
                  :text-selection="textSelection"
                  :document-search="documentSearch"
                  @add-search-matches="(type: string) => reviewStore.addSearchMatchesAsEntities(documentSearch.actionableMatches.value, type)"
                />
              </div>
              <div class="lg:col-span-4">
                <PseudonymizedTextPanel
                  ref="pseudoPanelRef"
                  :segments="reviewStore.segments"
                  :entities-by-segment="reviewStore.entitiesBySegment"
                  :document-search="documentSearch"
                  @complete-review="showCompleteReviewModal = true"
                />
              </div>
              <div class="lg:col-span-4">
                <WorkbenchRightRail
                  :text-selection="textSelection"
                />
              </div>
            </template>

            <!-- Klartext mode: 2-column layout -->
            <template v-else>
              <div class="lg:col-span-8">
                <WorkbenchDocumentViewer
                  :segments="reviewStore.segments"
                  :entities-by-segment="reviewStore.entitiesBySegment"
                  :display-mode="reviewStore.viewMode"
                  :text-selection="textSelection"
                />
              </div>
              <div class="lg:col-span-4">
                <WorkbenchRightRail
                  :text-selection="textSelection"
                />
              </div>
            </template>
          </div>
        </main>

        <CompleteReviewModal
          v-model:open="showCompleteReviewModal"
          @continue="showCompleteReviewModal = false"
          @end="handleEndReview"
        />

        <LlmScanModal
          v-model:open="showLlmScanModal"
          :job-id="jobId"
        />

        <LlmScanModal
          v-model:open="showRescanModal"
          :job-id="jobId"
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
    </template>

    <PlaygroundFooter />
  </div>
</template>
