import { apiDownloadFile, apiRequest } from './client'
export type SummaryType = 'facility' | 'event'
export type SummaryKind = 'all' | 'event' | 'project'
export interface SummaryFilter { fromDate: string; toDate: string; kind: SummaryKind }
export interface ReportSummary {
  type: SummaryType
  fromDate: string
  toDate: string
  recordCount: number
  attendance: number
  recordsWithAttendance: number
  eventCount: number
  projectCount: number
  sourceUpdatedAtUtc: string | null
  rows: { key: string; name: string; category: string; recordCount: number; attendance: number | null }[]
  months: { year: number; month: number; recordCount: number; attendance: number }[]
}
const path = (type: SummaryType, filter: SummaryFilter) => `/api/events/report-summaries/${type}?${new URLSearchParams({ ...filter })}`
export const getReportSummary = (type: SummaryType, filter: SummaryFilter, signal: AbortSignal) => apiRequest<ReportSummary>(path(type, filter), { signal })
export const downloadSummaryWord = (type: SummaryType, filter: SummaryFilter) => apiDownloadFile(path(type, filter).replace('?', '/word?'))
