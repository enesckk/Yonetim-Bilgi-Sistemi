import { apiDownloadFile, apiFetchBlob, apiRequest, apiUploadJson, getAccessToken } from './client'
export type ActivityReportInput = {
  title: string
  fromDate: string
  toDate: string
  dateBasis: 'event' | 'record'
  kind: 'all' | 'event' | 'project'
}
export type ActivityReportSummary = ActivityReportInput & {
  id: string
  revision: number
  sourceCount: number
  sourceAttendance: number
  createdAtUtc: string
}
export type ReportPage = {
  id: string
  sourceEventId?: string | null
  title: string
  body: string
  meta: string
  layout: 'single' | 'pair' | 'text'
  imageIds: string[]
  showMonthlyChart?: boolean
}
export type ReportSource = {
  id: string
  title: string
  dateUtc: string
  recordedAtUtc?: string
  status: string
  category: string
  attendance: number | null
}
export type ReportDocument = {
  organization: string
  department: string
  accent: string
  pages: ReportPage[]
  sources: ReportSource[]
  recordsWithoutImages: number
}
export type ReportAsset = {
  id: string
  fileName: string
  contentType: string
  sourceMediaId?: string | null
}
export type ActivityReportDetail = {
  report: ActivityReportSummary
  document: ReportDocument
  assets: ReportAsset[]
}
const root = '/api/events/reports'
export const listActivityReports = () => apiRequest<ActivityReportSummary[]>(root)
const pendingReports = new Map<string, Promise<ActivityReportDetail>>()
export function preloadActivityReport(id: string) {
  const key = `${getAccessToken()}:${id}`
  if (pendingReports.has(key)) return
  const request = apiRequest<ActivityReportDetail>(`${root}/${id}`, { signal: AbortSignal.timeout(60000) })
  pendingReports.set(key, request)
  void request.catch(() => pendingReports.delete(key))
  setTimeout(() => { if (pendingReports.get(key) === request) pendingReports.delete(key) }, 15000)
}
export function getActivityReport(id: string) {
  const key = `${getAccessToken()}:${id}`
  const pending = pendingReports.get(key)
  pendingReports.delete(key)
  return pending ?? apiRequest<ActivityReportDetail>(`${root}/${id}`, { signal: AbortSignal.timeout(60000) })
}
export const generateActivityReport = (input: ActivityReportInput) =>
  apiRequest<{ id: string }>(root, { method: 'POST', body: input })
export const saveActivityReport = (detail: ActivityReportDetail) =>
  apiRequest<ActivityReportDetail>(`${root}/${detail.report.id}`, {
    method: 'PUT',
    body: {
      revision: detail.report.revision,
      title: detail.report.title,
      document: detail.document,
    },
  })
export const getReportAsset = (reportId: string, assetId: string) =>
  apiFetchBlob(`${root}/${reportId}/assets/${assetId}`)
export function uploadReportAsset(id: string, file: File) {
  const f = new FormData()
  f.append('file', file)
  return apiUploadJson<ReportAsset>(`${root}/${id}/assets`, f)
}
export const downloadActivityReport = (id: string, kind: 'pdf' | 'excel' | 'word') =>
  apiDownloadFile(`${root}/${id}/${kind}`)
