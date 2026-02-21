import apiClient from './client'
import { USE_MOCKS } from './mocks/index'
import type {
  ReviewDataResponse,
  UpdateEntityRequest,
  EntityDto,
  AddEntityRequest,
  LlmScanResponse,
  LlmScanRequest,
  UpdateSegmentRequest,
} from './types'

export async function getReviewData(jobId: string): Promise<ReviewDataResponse> {
  if (USE_MOCKS) {
    const { mockGetReviewData } = await import('./mocks/review')
    return mockGetReviewData(jobId)
  }
  const response = await apiClient.get<ReviewDataResponse>(`/jobs/${jobId}/review`)
  return response.data
}

export async function updateEntity(
  jobId: string,
  entityId: string,
  request: UpdateEntityRequest,
): Promise<EntityDto> {
  if (USE_MOCKS) {
    const { mockUpdateEntity } = await import('./mocks/review')
    return mockUpdateEntity(jobId, entityId, request)
  }
  const response = await apiClient.patch<EntityDto>(
    `/jobs/${jobId}/entities/${entityId}`,
    request,
  )
  return response.data
}

export async function deleteEntity(jobId: string, entityId: string): Promise<void> {
  await apiClient.delete(`/jobs/${jobId}/entities/${entityId}`)
}

export async function addEntity(
  jobId: string,
  request: AddEntityRequest,
): Promise<EntityDto> {
  if (USE_MOCKS) {
    const { mockAddEntity } = await import('./mocks/review')
    return mockAddEntity(jobId, request)
  }
  const response = await apiClient.post<EntityDto>(`/jobs/${jobId}/entities`, request)
  return response.data
}

export async function deleteAllEntities(jobId: string): Promise<void> {
  await apiClient.delete(`/jobs/${jobId}/entities`)
}

export async function completeReview(jobId: string): Promise<void> {
  await apiClient.post(`/jobs/${jobId}/complete-review`)
}

export async function reopenReview(jobId: string): Promise<void> {
  await apiClient.post(`/jobs/${jobId}/reopen-review`)
}

export async function updateSegment(
  jobId: string,
  segmentId: string,
  request: UpdateSegmentRequest,
): Promise<void> {
  await apiClient.patch(`/jobs/${jobId}/segments/${segmentId}`, request)
}

export async function startLlmScan(jobId: string, options?: LlmScanRequest): Promise<LlmScanResponse> {
  const response = await apiClient.post<LlmScanResponse>(`/jobs/${jobId}/llm-scan`, options)
  return response.data
}

export async function getLlmScanStatus(jobId: string): Promise<LlmScanResponse> {
  const response = await apiClient.get<LlmScanResponse>(`/jobs/${jobId}/llm-scan`)
  return response.data
}

export async function cancelLlmScan(jobId: string): Promise<void> {
  await apiClient.delete(`/jobs/${jobId}/llm-scan`)
}

export async function startRescan(jobId: string): Promise<LlmScanResponse> {
  const response = await apiClient.post<LlmScanResponse>(`/jobs/${jobId}/rescan`)
  return response.data
}

export async function getRescanStatus(jobId: string): Promise<LlmScanResponse> {
  const response = await apiClient.get<LlmScanResponse>(`/jobs/${jobId}/rescan`)
  return response.data
}

export async function cancelRescan(jobId: string): Promise<void> {
  await apiClient.delete(`/jobs/${jobId}/rescan`)
}
