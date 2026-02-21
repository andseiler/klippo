<script setup lang="ts">
import { onMounted, onUnmounted, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useJobsStore } from '../stores/jobs'
import JobTable from '../components/dashboard/JobTable.vue'
import AppPagination from '../components/ui/AppPagination.vue'
import AppButton from '../components/ui/AppButton.vue'

const { t } = useI18n()
const jobsStore = useJobsStore()

let pollInterval: ReturnType<typeof setInterval> | null = null

const hasProcessingJobs = computed(() =>
  jobsStore.jobs.some((j) => j.status === 'created' || j.status === 'processing'),
)

function startPolling() {
  if (pollInterval) return
  pollInterval = setInterval(() => {
    jobsStore.silentFetchJobs()
  }, 5000)
}

function stopPolling() {
  if (pollInterval) {
    clearInterval(pollInterval)
    pollInterval = null
  }
}

watch(hasProcessingJobs, (has) => {
  if (has) {
    startPolling()
  } else {
    stopPolling()
  }
}, { immediate: true })

onMounted(() => {
  jobsStore.fetchJobs()
})

onUnmounted(() => {
  stopPolling()
})

function onPageChange(newPage: number) {
  jobsStore.page = newPage
  jobsStore.fetchJobs()
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-gray-100">
        {{ t('dashboard.title') }}
      </h1>
      <RouterLink to="/upload">
        <AppButton>{{ t('dashboard.uploadAction') }}</AppButton>
      </RouterLink>
    </div>

    <!-- Search -->
    <div>
      <input
        v-model="jobsStore.searchQuery"
        type="text"
        :placeholder="t('dashboard.searchPlaceholder')"
        class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
      />
    </div>

    <!-- Job table -->
    <div class="bg-white dark:bg-gray-800 rounded-lg shadow border border-gray-200 dark:border-gray-700">
      <JobTable />
    </div>

    <!-- Pagination -->
    <AppPagination
      v-if="jobsStore.totalCount > jobsStore.pageSize"
      :page="jobsStore.page"
      :total-pages="jobsStore.totalPages"
      :total-count="jobsStore.totalCount"
      :page-size="jobsStore.pageSize"
      @update:page="onPageChange"
    />
  </div>
</template>
