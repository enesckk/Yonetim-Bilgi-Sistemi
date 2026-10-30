import { apiDownloadFile, apiFetchBlob, apiRequest, apiUploadJson } from './client'
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
export const getActivityReport = (id: string) => apiRequest<ActivityReportDetail>(`${root}/${id}`)
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
export const downloadActivityReport = (id: string, kind: 'pdf' | 'excel') =>
  apiDownloadFile(`${root}/${id}/${kind}`)
