import apiClient from './client'
import { USE_MOCKS } from './mocks/index'
import type { JobListResponse, JobResponse, CreateJobRequest, UpdateJobRequest } from './types'

export async function listJobs(
  page: number = 1,
  pageSize: number = 20,
): Promise<JobListResponse> {
  const response = await apiClient.get<JobListResponse>('/jobs', {
    params: { page, pageSize },
  })
  return response.data
}

export async function getJob(id: string): Promise<JobResponse> {
  if (USE_MOCKS) {
    const { mockGetJob } = await import('./mocks/jobs')
    return mockGetJob(id)
  }
  const response = await apiClient.get<JobResponse>(`/jobs/${id}`)
  return response.data
}

export async function deleteJob(id: string): Promise<void> {
  if (USE_MOCKS) {
    const { mockDeleteJob } = await import('./mocks/jobs')
    return mockDeleteJob(id)
  }
  await apiClient.delete(`/jobs/${id}`)
}

export async function updateJob(id: string, request: UpdateJobRequest): Promise<JobResponse> {
  const response = await apiClient.patch<JobResponse>(`/jobs/${id}`, request)
  return response.data
}

export async function cancelJob(id: string): Promise<void> {
  await apiClient.post(`/jobs/${id}/cancel`)
}

export async function createJob(request: CreateJobRequest): Promise<JobResponse> {
  if (USE_MOCKS) {
    const { mockCreateJob } = await import('./mocks/jobs')
    return mockCreateJob(request)
  }

  const formData = new FormData()
  formData.append('file', request.file)

  const response = await apiClient.post<JobResponse>('/jobs', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return response.data
}
