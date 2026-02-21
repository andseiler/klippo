<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useJobsStore } from '../../stores/jobs'
import JobRow from './JobRow.vue'
import AppSpinner from '../ui/AppSpinner.vue'
import AppAlert from '../ui/AppAlert.vue'

const { t } = useI18n()
const jobsStore = useJobsStore()
</script>

<template>
  <div>
    <!-- Loading -->
    <div v-if="jobsStore.loading" class="flex justify-center py-12">
      <AppSpinner />
    </div>

    <!-- Error -->
    <AppAlert v-else-if="jobsStore.error" variant="error">
      {{ jobsStore.error }}
    </AppAlert>

    <!-- Empty -->
    <div
      v-else-if="jobsStore.filteredJobs.length === 0"
      class="text-center py-12"
    >
      <p class="text-gray-500 dark:text-gray-400 text-lg">{{ t('dashboard.noJobs') }}</p>
      <p class="text-gray-400 dark:text-gray-500 text-sm mt-1">{{ t('dashboard.noJobsDescription') }}</p>
    </div>

    <!-- Table (desktop) -->
    <div v-else class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="border-b border-gray-200 dark:border-gray-700">
            <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {{ t('dashboard.table.fileName') }}
            </th>
            <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {{ t('dashboard.table.status') }}
            </th>
            <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {{ t('dashboard.table.createdAt') }}
            </th>
            <th class="px-4 py-3 w-16">
              <span class="sr-only">{{ t('dashboard.table.actions') }}</span>
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
          <JobRow v-for="job in jobsStore.filteredJobs" :key="job.id" :job="job" />
        </tbody>
      </table>
    </div>
  </div>
</template>
