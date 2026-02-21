<script setup lang="ts">
import { computed, ref, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ReviewSummary } from '../../api/types'
import type { JobStatus } from '../../api/types'
import type { ViewMode } from '../../stores/review'
import { getJobStatusMeta } from '../../constants/jobStatuses'
import AppButton from '../ui/AppButton.vue'
import WorkbenchModeToggle from './WorkbenchModeToggle.vue'

const props = withDefaults(defineProps<{
  jobId: string
  fileName: string
  status: string
  summary: ReviewSummary
  saving: boolean
  displayMode: ViewMode
  backTo?: string
  playground?: boolean
}>(), {
  backTo: '/dashboard',
  playground: false,
})

const emit = defineEmits<{
  llmScan: []
  rescan: []
  back: []
  'update:fileName': [value: string]
  'update:displayMode': [value: ViewMode]
}>()

const isEditingName = ref(false)
const editNameValue = ref('')
const nameInputRef = ref<HTMLInputElement | null>(null)

function startEditingName() {
  isEditingName.value = true
  editNameValue.value = props.fileName
  nextTick(() => {
    nameInputRef.value?.focus()
    nameInputRef.value?.select()
  })
}

function saveFileName() {
  const trimmed = editNameValue.value.trim()
  if (trimmed && trimmed !== props.fileName) {
    emit('update:fileName', trimmed)
  }
  isEditingName.value = false
}

function cancelEditName() {
  isEditingName.value = false
}

const { t } = useI18n()

const statusMeta = computed(() => {
  return getJobStatusMeta(props.status as JobStatus) ?? { colorClass: 'bg-gray-100 text-gray-700', iconName: 'circle' }
})

const POST_PROCESSING_STATUSES = ['readyreview', 'inreview', 'pseudonymized', 'scanpassed', 'scanfailed', 'depseudonymized']
const showLlmScanButton = computed(() => POST_PROCESSING_STATUSES.includes(props.status))
</script>

<template>
  <header class="flex items-center gap-3 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 flex-shrink-0">
    <!-- Back button -->
    <AppButton v-if="playground" variant="ghost" size="sm" class="flex-shrink-0" @click="emit('back')">
      &larr; {{ t('review.workbench.header.backToPlayground') }}
    </AppButton>
    <RouterLink v-else :to="backTo" class="flex-shrink-0">
      <AppButton variant="ghost" size="sm">
        &larr; {{ t('review.workbench.header.backToDashboard') }}
      </AppButton>
    </RouterLink>

    <!-- File name + badges (hidden in playground mode) -->
    <div v-if="!playground" class="flex items-center gap-2 min-w-0">
      <input
        v-if="isEditingName"
        ref="nameInputRef"
        v-model="editNameValue"
        class="text-sm font-semibold text-gray-900 dark:text-gray-100 bg-white dark:bg-gray-800 border border-blue-500 rounded px-1 py-0.5 outline-none min-w-[120px]"
        @blur="saveFileName"
        @keydown.enter="saveFileName"
        @keydown.escape="cancelEditName"
      />
      <span
        v-else
        class="text-sm font-semibold text-gray-900 dark:text-gray-100 truncate cursor-pointer hover:underline"
        @click="startEditingName"
      >
        {{ fileName }}
      </span>
    </div>

    <!-- Center: Mode toggle -->
    <div class="flex-1 flex justify-center">
      <WorkbenchModeToggle
        :mode="displayMode"
        @update:mode="emit('update:displayMode', $event)"
      />
    </div>

    <!-- Compact stats -->
    <div class="hidden md:flex items-center gap-3 text-xs text-gray-500 dark:text-gray-400 flex-shrink-0">
      <span>{{ t('review.workbench.header.entities', { count: summary.totalEntities }) }}</span>
    </div>

    <!-- Status chip -->
    <span :class="['inline-flex items-center px-2 py-0.5 text-xs font-medium rounded-full flex-shrink-0', statusMeta.colorClass]">
      {{ t(`job.status.${status}`, status) }}
    </span>

    <!-- Action buttons -->
    <div class="flex items-center gap-2 flex-shrink-0">
      <AppButton
        v-if="showLlmScanButton"
        variant="secondary"
        size="sm"
        @click="emit('rescan')"
      >
        {{ t('review.workbench.header.rescan') }}
      </AppButton>
      <AppButton
        v-if="showLlmScanButton"
        variant="secondary"
        size="sm"
        @click="emit('llmScan')"
      >
        {{ t('review.workbench.header.llmScan') }}
      </AppButton>
    </div>
  </header>
</template>
