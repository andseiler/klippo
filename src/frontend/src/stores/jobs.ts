import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { listJobs, getJob, createJob, deleteJob as apiDeleteJob } from '../api/jobs'
import type { JobResponse, CreateJobRequest } from '../api/types'

export const useJobsStore = defineStore('jobs', () => {
  const jobs = ref<JobResponse[]>([])
  const currentJob = ref<JobResponse | null>(null)
  const totalCount = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const searchQuery = ref('')

  const filteredJobs = computed(() => {
    const q = searchQuery.value.trim().toLowerCase()
    if (!q) return jobs.value
    return jobs.value.filter(
      (j) =>
        j.fileName.toLowerCase().includes(q) ||
        j.status.toLowerCase().includes(q),
    )
  })

  const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))

  async function fetchJobs() {
    loading.value = true
    error.value = null
    try {
      const response = await listJobs(page.value, pageSize.value)
      jobs.value = response.items
      totalCount.value = response.totalCount
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load jobs'
    } finally {
      loading.value = false
    }
  }

  async function fetchJob(id: string) {
    loading.value = true
    error.value = null
    try {
      currentJob.value = await getJob(id)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load job'
    } finally {
      loading.value = false
    }
  }

  async function submitJob(request: CreateJobRequest): Promise<JobResponse> {
    loading.value = true
    error.value = null
    try {
      const job = await createJob(request)
      return job
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to create job'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function silentFetchJobs() {
    error.value = null
    try {
      const response = await listJobs(page.value, pageSize.value)
      jobs.value = response.items
      totalCount.value = response.totalCount
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load jobs'
    }
  }

  async function deleteJob(id: string) {
    const backup = jobs.value
    const backupCount = totalCount.value
    jobs.value = jobs.value.filter((j) => j.id !== id)
    totalCount.value--
    try {
      await apiDeleteJob(id)
    } catch (e) {
      jobs.value = backup
      totalCount.value = backupCount
      error.value = e instanceof Error ? e.message : 'Failed to delete job'
    }
  }

  return {
    jobs,
    currentJob,
    totalCount,
    page,
    pageSize,
    loading,
    error,
    searchQuery,
    filteredJobs,
    totalPages,
    fetchJobs,
    fetchJob,
    submitJob,
    deleteJob,
    silentFetchJobs,
  }
})
