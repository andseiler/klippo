<script setup lang="ts">
import { ref, computed, onUnmounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../../stores/review'
import {
  startLlmScan, getLlmScanStatus, cancelLlmScan,
  startRescan, getRescanStatus, cancelRescan,
  deleteEntity,
} from '../../api/review'
import type { LlmScanResponse } from '../../api/types'
import AppModal from '../ui/AppModal.vue'
import AppButton from '../ui/AppButton.vue'
import AppProgress from '../ui/AppProgress.vue'
import AppAlert from '../ui/AppAlert.vue'

const DEFAULT_INSTRUCTIONS: Record<string, string> = {
  en: `Analyze the following text for personally identifiable information (PII) that has NOT \
already been detected. Focus on contextual PII like addresses, relationships, health \
information, financial details, or any other sensitive personal data.`,
  de: `Analysiere den folgenden Text auf personenbezogene Daten (PII), die NOCH NICHT erkannt \
wurden. Konzentriere dich auf kontextuelle PII wie Adressen, Beziehungen, \
Gesundheitsinformationen, finanzielle Details oder andere sensible persönliche Daten.`,
}

const props = withDefaults(defineProps<{
  open: boolean
  jobId: string
  mode?: 'llm' | 'rescan'
}>(), {
  mode: 'llm',
})

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { t, locale } = useI18n()
const reviewStore = useReviewStore()

const currentLang = computed(() => (locale.value === 'en' ? 'en' : 'de'))
const isRescan = computed(() => props.mode === 'rescan')

const modalTitle = computed(() =>
  isRescan.value
    ? t('review.workbench.rescan.title')
    : t('review.workbench.llmScan.title')
)

type Phase = 'configure' | 'scanning' | 'results'
const phase = ref<Phase>('configure')
const instructions = ref(DEFAULT_INSTRUCTIONS[currentLang.value])

const scanData = ref<LlmScanResponse | null>(null)
const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)
const selectedDetections = ref<Set<number>>(new Set())
const applying = ref(false)
const cancellingScan = ref(false)

const progress = computed(() => {
  if (!scanData.value || scanData.value.totalSegments === 0) return 0
  return Math.round((scanData.value.processedSegments / scanData.value.totalSegments) * 100)
})

function stopPolling() {
  if (pollTimer.value) {
    clearInterval(pollTimer.value)
    pollTimer.value = null
  }
}

function getStatusFn() {
  return isRescan.value ? getRescanStatus : getLlmScanStatus
}

function getCancelFn() {
  return isRescan.value ? cancelRescan : cancelLlmScan
}

async function startScan() {
  phase.value = 'scanning'
  try {
    if (isRescan.value) {
      scanData.value = await startRescan(props.jobId)
    } else {
      scanData.value = await startLlmScan(props.jobId, {
        instructions: instructions.value,
        language: currentLang.value,
      })
    }

    const pollFn = getStatusFn()
    pollTimer.value = setInterval(async () => {
      try {
        const status = await pollFn(props.jobId)
        scanData.value = status
        if (status.status === 'completed' || status.status === 'failed' || status.status === 'cancelled') {
          stopPolling()
          phase.value = 'results'
          if (status.status === 'completed') {
            selectedDetections.value = new Set(status.detections.map((_, i) => i))
          }
        }
      } catch {
        stopPolling()
      }
    }, 2000)
  } catch (e) {
    scanData.value = {
      status: 'failed',
      processedSegments: 0,
      totalSegments: 0,
      detections: [],
      error: e instanceof Error ? e.message : 'Failed to start scan',
    }
    phase.value = 'results'
  }
}

async function handleCancelScan() {
  cancellingScan.value = true
  try {
    await getCancelFn()(props.jobId)
  } catch {
    // Polling will pick up status change
  } finally {
    cancellingScan.value = false
  }
}

const allSelected = computed(() => {
  if (!scanData.value) return false
  return scanData.value.detections.length > 0 && selectedDetections.value.size === scanData.value.detections.length
})

function toggleSelectAll() {
  if (!scanData.value) return
  if (allSelected.value) {
    selectedDetections.value = new Set()
  } else {
    selectedDetections.value = new Set(scanData.value.detections.map((_, i) => i))
  }
}

function toggleDetection(idx: number) {
  const next = new Set(selectedDetections.value)
  if (next.has(idx)) {
    next.delete(idx)
  } else {
    next.add(idx)
  }
  selectedDetections.value = next
}

async function handleApply() {
  if (!scanData.value) return
  applying.value = true
  try {
    // Delete unchecked detections
    const unchecked = scanData.value.detections.filter((_, i) => !selectedDetections.value.has(i))
    for (const det of unchecked) {
      if (det.entityId) {
        await deleteEntity(props.jobId, det.entityId)
      }
    }
    // Close modal BEFORE refetch — fetchReviewData sets loading=true,
    // which unmounts this component via v-if/v-else in the parent
    emit('update:open', false)
    await reviewStore.fetchReviewData(props.jobId)
  } finally {
    applying.value = false
  }
}

function handleClose() {
  stopPolling()
  emit('update:open', false)
}

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    scanData.value = null
    selectedDetections.value = new Set()
    cancellingScan.value = false
    if (isRescan.value) {
      // Rescan skips configure — go straight to scanning
      phase.value = 'scanning'
      startScan()
    } else {
      phase.value = 'configure'
      instructions.value = DEFAULT_INSTRUCTIONS[currentLang.value]
    }
  } else {
    stopPolling()
  }
})

onUnmounted(() => {
  stopPolling()
})
</script>

<template>
  <AppModal
    :open="open"
    :title="modalTitle"
    persistent
    size="wide"
    @update:open="emit('update:open', $event)"
  >
    <div class="space-y-4">
      <!-- Configure phase (LLM mode only) -->
      <template v-if="phase === 'configure' && !isRescan">
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            {{ t('review.workbench.llmScan.instructionsLabel') }}
          </label>
          <textarea
            v-model="instructions"
            rows="12"
            class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm text-gray-900 dark:text-gray-100 p-2 font-mono resize-y"
          />
        </div>

        <div class="flex justify-end gap-3 pt-2">
          <AppButton variant="secondary" @click="handleClose">
            {{ t('review.workbench.llmScan.close') }}
          </AppButton>
          <AppButton @click="startScan">
            {{ t('review.workbench.llmScan.startScan') }}
          </AppButton>
        </div>
      </template>

      <!-- Scanning phase -->
      <template v-else-if="phase === 'scanning'">
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ t(isRescan ? 'review.workbench.rescan.scanning' : 'review.workbench.llmScan.scanning', {
            current: scanData?.processedSegments ?? 0,
            total: scanData?.totalSegments ?? 0
          }) }}
        </p>
        <AppProgress :value="progress" />
        <div class="flex justify-end pt-2">
          <AppButton
            variant="secondary"
            size="sm"
            :loading="cancellingScan"
            :disabled="cancellingScan"
            @click="handleCancelScan"
          >
            {{ t('review.workbench.llmScan.cancel') }}
          </AppButton>
        </div>
      </template>

      <!-- Results phase -->
      <template v-else-if="phase === 'results'">
        <!-- Error state -->
        <AppAlert v-if="scanData?.status === 'failed'" variant="error">
          {{ t(isRescan ? 'review.workbench.rescan.error' : 'review.workbench.llmScan.error') }}
          <template v-if="scanData.error">: {{ scanData.error }}</template>
        </AppAlert>

        <!-- Cancelled state -->
        <AppAlert v-else-if="scanData?.status === 'cancelled'" variant="warning">
          {{ t('review.workbench.llmScan.cancelled') }}
        </AppAlert>

        <!-- Completed state -->
        <template v-else-if="scanData?.status === 'completed'">
          <!-- Results summary -->
          <div v-if="scanData.detections.length > 0" class="space-y-3">
            <div class="rounded-md bg-blue-50 dark:bg-blue-900/20 p-3">
              <p class="text-sm font-medium text-blue-700 dark:text-blue-300">
                {{ t(isRescan ? 'review.workbench.rescan.completed' : 'review.workbench.llmScan.completed', { count: scanData.detections.length }) }}
              </p>
            </div>

            <!-- Detections table -->
            <div class="border border-gray-200 dark:border-gray-700 rounded-md overflow-hidden max-h-[50vh] overflow-y-auto">
              <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead class="bg-gray-50 dark:bg-gray-800 sticky top-0">
                  <tr>
                    <th class="px-3 py-2 text-center w-8">
                      <input
                        type="checkbox"
                        :checked="allSelected"
                        class="rounded border-gray-300 dark:border-gray-600"
                        @change="toggleSelectAll"
                      />
                    </th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">
                      {{ t('review.workbench.llmScan.detectionType') }}
                    </th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">
                      {{ t('review.workbench.llmScan.detectionText') }}
                    </th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">
                      {{ t('review.workbench.llmScan.detectionConfidence') }}
                    </th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="(det, idx) in scanData.detections" :key="idx">
                    <td class="px-3 py-2 text-center">
                      <input
                        type="checkbox"
                        :checked="selectedDetections.has(idx)"
                        class="rounded border-gray-300 dark:border-gray-600"
                        @change="toggleDetection(idx)"
                      />
                    </td>
                    <td class="px-3 py-2 text-sm text-gray-900 dark:text-gray-100">
                      {{ det.entityType }}
                    </td>
                    <td class="px-3 py-2 text-sm text-gray-900 dark:text-gray-100 font-mono truncate max-w-[200px]">
                      {{ det.originalText ?? '\u2014' }}
                    </td>
                    <td class="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">
                      {{ (det.confidence * 100).toFixed(0) }}%
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- No results -->
          <div v-else class="rounded-md bg-green-50 dark:bg-green-900/20 p-4">
            <p class="text-sm text-green-700 dark:text-green-300">
              {{ t(isRescan ? 'review.workbench.rescan.noResults' : 'review.workbench.llmScan.noResults') }}
            </p>
          </div>
        </template>

        <!-- Actions -->
        <div class="flex justify-end gap-3 pt-2">
          <AppButton
            v-if="scanData?.status === 'completed' && scanData.detections.length > 0"
            :loading="applying"
            :disabled="applying"
            @click="handleApply"
          >
            {{ t('review.workbench.llmScan.apply') }}
          </AppButton>
          <AppButton
            variant="secondary"
            @click="handleClose"
          >
            {{ t('review.workbench.llmScan.close') }}
          </AppButton>
        </div>
      </template>
    </div>
  </AppModal>
</template>
