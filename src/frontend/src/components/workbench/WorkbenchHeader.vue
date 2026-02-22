<script setup lang="ts">
import { computed, ref, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import type { JobStatus } from '../../api/types'
import type { ViewMode } from '../../stores/review'
import { getJobStatusMeta } from '../../constants/jobStatuses'
import AppButton from '../ui/AppButton.vue'
import WorkbenchModeToggle from './WorkbenchModeToggle.vue'

const props = withDefaults(defineProps<{
  jobId: string
  fileName: string
  status: string
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
  <header class="flex flex-col gap-2 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 flex-shrink-0">
    <!-- Desktop: three-column layout (left / center / right) -->
    <div class="hidden lg:grid lg:grid-cols-[1fr_auto_1fr] lg:items-center lg:gap-3">
      <!-- Left: back, filename, status -->
      <div class="flex items-center gap-3 min-w-0">
        <AppButton v-if="playground" variant="ghost" size="sm" class="flex-shrink-0" @click="emit('back')">
          &larr; {{ t('review.workbench.header.backToPlayground') }}
        </AppButton>
        <RouterLink v-else :to="backTo" class="flex-shrink-0">
          <AppButton variant="ghost" size="sm">
            &larr; {{ t('review.workbench.header.backToDashboard') }}
          </AppButton>
        </RouterLink>

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
            class="text-sm font-semibold text-gray-900 dark:text-gray-100 truncate max-w-[200px] cursor-pointer hover:underline"
            @click="startEditingName"
          >
            {{ fileName }}
          </span>
        </div>

        <span :class="['inline-flex items-center px-2 py-0.5 text-xs font-medium rounded-full flex-shrink-0', statusMeta.colorClass]">
          {{ t(`job.status.${status}`, status) }}
        </span>
      </div>

      <!-- Center: mode toggle -->
      <WorkbenchModeToggle
        :mode="displayMode"
        @update:mode="emit('update:displayMode', $event)"
      />

      <!-- Right: action buttons -->
      <div class="flex items-center justify-end gap-3">
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
    </div>

    <!-- Mobile: top row (back left, status + actions centered) -->
    <div class="grid grid-cols-[auto_1fr] items-center gap-2 lg:hidden">
      <AppButton v-if="playground" variant="ghost" size="sm" @click="emit('back')">
        &larr;
      </AppButton>
      <RouterLink v-else :to="backTo">
        <AppButton variant="ghost" size="sm">&larr;</AppButton>
      </RouterLink>

      <div class="flex items-center justify-center gap-2">
        <span :class="['inline-flex items-center px-2 py-0.5 text-xs font-medium rounded-full', statusMeta.colorClass]">
          {{ t(`job.status.${status}`, status) }}
        </span>
        <AppButton
          v-if="showLlmScanButton"
          variant="secondary"
          size="sm"
          @click="emit('rescan')"
        >
          &#x21bb;
        </AppButton>
        <AppButton
          v-if="showLlmScanButton"
          variant="secondary"
          size="sm"
          @click="emit('llmScan')"
        >
          AI
        </AppButton>
      </div>
    </div>

    <!-- Mobile: second row (mode toggle, centered) -->
    <div class="flex justify-center lg:hidden">
      <WorkbenchModeToggle
        :mode="displayMode"
        @update:mode="emit('update:displayMode', $event)"
      />
    </div>
  </header>
</template>
