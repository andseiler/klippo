<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import type { JobResponse } from '../../api/types'
import StatusBadge from './StatusBadge.vue'
import AppSpinner from '../ui/AppSpinner.vue'
import InlineDeleteButton from '../ui/InlineDeleteButton.vue'
import { useJobsStore } from '../../stores/jobs'

interface Props {
  job: JobResponse
}

const props = defineProps<Props>()
const router = useRouter()
const { t } = useI18n()
const jobsStore = useJobsStore()

const isProcessing = computed(() => props.job.status === 'processing')
const isClickable = computed(() => !isProcessing.value && props.job.status !== 'failed')

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString()
}

function navigateToReview() {
  if (!isClickable.value) return
  router.push(`/jobs/${props.job.id}/review`)
}
</script>

<template>
  <tr
    :class="[
      'transition-colors',
      isClickable ? 'hover:bg-gray-50 dark:hover:bg-gray-700/50 cursor-pointer' : 'opacity-75',
    ]"
    @click="navigateToReview"
  >
    <td class="px-4 py-3 text-sm text-gray-900 dark:text-gray-100 font-medium">
      {{ job.fileName }}
    </td>
    <td class="px-4 py-3">
      <div class="flex items-center gap-2">
        <StatusBadge :status="job.status" />
        <AppSpinner v-if="isProcessing" size="sm" />
      </div>
      <p v-if="job.errorMessage" class="text-xs text-red-600 dark:text-red-400 mt-1 truncate max-w-[200px]" :title="job.errorMessage">
        {{ job.errorMessage }}
      </p>
    </td>
    <td class="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
      {{ formatDate(job.createdAt) }}
    </td>
    <td class="px-4 py-3">
      <InlineDeleteButton @confirm="jobsStore.deleteJob(job.id)" />
    </td>
  </tr>
</template>
