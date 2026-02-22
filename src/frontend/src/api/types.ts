export type JobStatus =
  | 'created'
  | 'processing'
  | 'readyreview'
  | 'inreview'
  | 'pseudonymized'
  | 'scanpassed'
  | 'scanfailed'
  | 'depseudonymized'
  | 'failed'
  | 'cancelled'

export interface LoginRequest {
  email: string
  password: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  userId: string
  email: string
  name: string
}

export interface JobResponse {
  id: string
  createdById: string
  status: JobStatus
  fileName: string
  fileType: string
  fileHash: string | null
  fileSizeBytes: number
  secondScanPassed: boolean
  createdAt: string
  processingStartedAt: string | null
  reviewStartedAt: string | null
  pseudonymizedAt: string | null
  errorMessage: string | null
}

export interface JobListResponse {
  items: JobResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateJobRequest {
  file: File
}

export interface UpdateJobRequest {
  fileName?: string
}

// Review types

export type ReviewStatus = 'pending' | 'confirmed' | 'addedmanual'
export type ConfidenceTier = 'HIGH' | 'MEDIUM' | 'LOW'

export interface SegmentDto {
  id: string
  segmentIndex: number
  textContent: string
  sourceType: string
  sourceLocation: string | null
}

export interface EntityDto {
  id: string
  segmentId: string
  text: string
  entityType: string
  startOffset: number
  endOffset: number
  confidence: number
  detectionSources: string[]
  confidenceTier: ConfidenceTier
  replacementPreview: string | null
  reviewStatus: ReviewStatus
}

export interface ReviewSummary {
  totalEntities: number
  highConfidence: number
  mediumConfidence: number
  lowConfidence: number
  confirmed: number
  manuallyAdded: number
  pending: number
}

export interface ReviewDataResponse {
  jobId: string
  status: string
  segments: SegmentDto[]
  entities: EntityDto[]
  summary: ReviewSummary
}

export interface UpdateEntityRequest {
  reviewStatus?: 'confirmed'
  entityType?: string
  startOffset?: number
  endOffset?: number
  replacementText?: string
}

export interface AddEntityRequest {
  segmentId: string
  text: string
  entityType: string
  startOffset: number
  endOffset: number
  replacementText?: string
}

// LLM Scan types

export interface UpdateSegmentRequest {
  textContent: string
  entityOffsets: EntityOffsetUpdate[]
}

export interface EntityOffsetUpdate {
  entityId: string
  startOffset: number
  endOffset: number
  text: string
}

export interface LlmScanRequest {
  instructions?: string
  language?: string
}

export interface LlmScanDetection {
  segmentId: string
  entityType: string
  startOffset: number
  endOffset: number
  confidence: number
  originalText: string | null
  entityId: string | null
}

export interface LlmScanResponse {
  status: string
  processedSegments: number
  totalSegments: number
  detections: LlmScanDetection[]
  error: string | null
}
