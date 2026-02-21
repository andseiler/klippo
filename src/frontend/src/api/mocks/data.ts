import type { JobResponse, JobStatus } from '../types'

let mockIdCounter = 100

export function createMockJob(overrides: Partial<JobResponse> = {}): JobResponse {
  mockIdCounter++
  const now = new Date().toISOString()
  return {
    id: `mock-${mockIdCounter}`,
    organizationId: 'org-1',
    createdById: 'user-1',
    status: 'created',
    fileName: 'document.pdf',
    fileType: 'application/pdf',
    fileHash: null,
    fileSizeBytes: 1024000,
    secondScanPassed: false,
    createdAt: now,
    processingStartedAt: null,
    reviewStartedAt: null,
    pseudonymizedAt: null,
    ...overrides,
  }
}

const statusConfigs: { status: JobStatus; fileName: string }[] = [
  { status: 'created', fileName: 'Arbeitsvertrag_Mueller.pdf' },
  { status: 'processing', fileName: 'Rechnung_2024_001.pdf' },
  { status: 'readyreview', fileName: 'Personalakte_Schmidt.docx' },
  { status: 'inreview', fileName: 'Befund_Patient_A.pdf' },
  { status: 'pseudonymized', fileName: 'Brief_Kunde_Weber.txt' },
  { status: 'scanpassed', fileName: 'Bilanz_Q4_2024.xlsx' },
  { status: 'scanfailed', fileName: 'Akte_123_Vertraulich.pdf' },
  { status: 'pseudonymized', fileName: 'Mietvertrag_Berlin.pdf' },
  { status: 'depseudonymized', fileName: 'Notizen_Meeting.txt' },
  { status: 'processing', fileName: 'Gehaltsabrechnung_Jan.pdf' },
  { status: 'readyreview', fileName: 'Steuerbescheid_2024.pdf' },
  { status: 'created', fileName: 'Anfrage_Partner_GmbH.docx' },
  { status: 'pseudonymized', fileName: 'Laborbericht_B.pdf' },
  { status: 'inreview', fileName: 'Klage_456_Entwurf.pdf' },
  { status: 'pseudonymized', fileName: 'Rechnung_2024_042.pdf' },
  { status: 'scanpassed', fileName: 'Liefervertrag_Nord.pdf' },
]

export const MOCK_JOBS: JobResponse[] = statusConfigs.map((cfg, i) => {
  const baseDate = new Date('2024-12-01T10:00:00Z')
  baseDate.setDate(baseDate.getDate() + i)
  const created = baseDate.toISOString()

  const processingDate = new Date(baseDate)
  processingDate.setHours(processingDate.getHours() + 1)

  const reviewDate = new Date(processingDate)
  reviewDate.setHours(reviewDate.getHours() + 2)

  const pseudoDate = new Date(reviewDate)
  pseudoDate.setHours(pseudoDate.getHours() + 1)

  const statusOrder = ['created', 'processing', 'readyreview', 'inreview', 'pseudonymized', 'scanpassed', 'scanfailed', 'depseudonymized']
  const statusIdx = statusOrder.indexOf(cfg.status)

  return createMockJob({
    id: `mock-job-${i + 1}`,
    status: cfg.status,
    fileName: cfg.fileName,
    fileType: cfg.fileName.endsWith('.pdf') ? 'application/pdf' : cfg.fileName.endsWith('.docx') ? 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' : cfg.fileName.endsWith('.xlsx') ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 'text/plain',
    fileSizeBytes: 500000 + Math.floor(Math.random() * 5000000),
    createdAt: created,
    processingStartedAt: statusIdx >= 1 ? processingDate.toISOString() : null,
    reviewStartedAt: statusIdx >= 3 ? reviewDate.toISOString() : null,
    pseudonymizedAt: statusIdx >= 4 ? pseudoDate.toISOString() : null,
  })
})
