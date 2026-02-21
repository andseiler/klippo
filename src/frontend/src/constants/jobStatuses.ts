import type { JobStatus } from '../api/types'

export interface JobStatusMeta {
  order: number
  colorClass: string
  iconName: string
}

const JOB_STATUS_META: Record<JobStatus, JobStatusMeta> = {
  created: { order: 0, colorClass: 'bg-gray-100 text-gray-700', iconName: 'circle-plus' },
  processing: { order: 1, colorClass: 'bg-blue-100 text-blue-700', iconName: 'loader' },
  readyreview: { order: 2, colorClass: 'bg-yellow-100 text-yellow-700', iconName: 'eye' },
  inreview: { order: 3, colorClass: 'bg-orange-100 text-orange-700', iconName: 'pencil' },
  pseudonymized: { order: 4, colorClass: 'bg-green-100 text-green-700', iconName: 'shield-check' },
  scanpassed: { order: 5, colorClass: 'bg-emerald-100 text-emerald-700', iconName: 'check-circle' },
  scanfailed: { order: 5, colorClass: 'bg-red-100 text-red-700', iconName: 'x-circle' },
  depseudonymized: { order: 6, colorClass: 'bg-indigo-100 text-indigo-700', iconName: 'undo' },
  failed: { order: -1, colorClass: 'bg-red-100 text-red-700', iconName: 'x-circle' },
  cancelled: { order: -1, colorClass: 'bg-gray-100 text-gray-700', iconName: 'x-circle' },
}

export function getJobStatusMeta(status: JobStatus): JobStatusMeta {
  return JOB_STATUS_META[status]
}

export const ALL_JOB_STATUSES: JobStatus[] = Object.keys(JOB_STATUS_META) as JobStatus[]

