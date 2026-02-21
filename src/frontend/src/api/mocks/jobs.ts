import type { JobResponse, CreateJobRequest } from '../types'
import { MOCK_JOBS, createMockJob } from './data'

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

export async function mockCreateJob(request: CreateJobRequest): Promise<JobResponse> {
  await delay(800 + Math.random() * 400)

  const job = createMockJob({
    status: 'created',
    fileName: request.file.name,
    fileType: request.file.type,
    fileSizeBytes: request.file.size,
  })

  MOCK_JOBS.unshift(job)
  return job
}

export async function mockDeleteJob(id: string): Promise<void> {
  await delay(400 + Math.random() * 200)
  const idx = MOCK_JOBS.findIndex((j) => j.id === id)
  if (idx !== -1) MOCK_JOBS.splice(idx, 1)
}

export async function mockGetJob(id: string): Promise<JobResponse> {
  await delay(300 + Math.random() * 200)

  const job = MOCK_JOBS.find((j) => j.id === id)
  if (!job) {
    throw new Error('Job not found')
  }
  return { ...job }
}
