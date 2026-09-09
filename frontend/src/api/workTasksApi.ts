import { apiRequest, apiUploadJson } from './client'

export type WorkTaskKind = 1 | 2
export type WorkTaskStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8

export interface WorkTaskUser {
  id: string
  displayName: string
  roleLabel: string
}

export interface WorkTaskAttachment {
  id: string
  fileName: string
  contentType: string
  uploadedByName: string
  createdAtUtc: string
}

export interface WorkTaskActivity {
  id: string
  action: string
  actionLabel: string
  note?: string | null
  actorName: string
  atUtc: string
}

export interface WorkTaskRow {
  id: string
  title: string
  kind: WorkTaskKind
  kindLabel: string
  status: WorkTaskStatus
  statusLabel: string
  createdByName: string
  assigneeName?: string | null
  unitName?: string | null
  dueOn?: string | null
  createdAtUtc: string
  attachmentCount: number
  canReview: boolean
  canSubmit: boolean
  canCancel: boolean
  canDelete: boolean
}

export interface WorkTaskDetail extends WorkTaskRow {
  description: string
  reviewNote?: string | null
  createdByUserId: string
  assigneeUserId?: string | null
  reviewerUserId?: string | null
  unitId?: string | null
  attachments: WorkTaskAttachment[]
  activities: WorkTaskActivity[]
}

export function fetchWorkTaskOptions() {
  return apiRequest<WorkTaskUser[]>('/api/work-tasks/options')
}

export function fetchWorkTasks(tab?: string) {
  const q = tab ? `?tab=${encodeURIComponent(tab)}` : ''
  return apiRequest<WorkTaskRow[]>(`/api/work-tasks${q}`)
}

export function fetchWorkTask(id: string) {
  return apiRequest<WorkTaskDetail>(`/api/work-tasks/${id}`)
}

export function createWorkTask(form: FormData) {
  return apiUploadJson<WorkTaskDetail>('/api/work-tasks', form)
}

export function submitWorkTask(id: string, form: FormData) {
  return apiUploadJson<WorkTaskDetail>(`/api/work-tasks/${id}/submit`, form)
}

export function reviewWorkTask(id: string, decision: 'approve' | 'reject' | 'revision', note?: string) {
  return apiRequest<WorkTaskDetail>(`/api/work-tasks/${id}/review`, {
    method: 'POST',
    body: { decision, note },
  })
}

export function cancelWorkTask(id: string, note?: string) {
  return apiRequest<WorkTaskDetail>(`/api/work-tasks/${id}/cancel`, {
    method: 'POST',
    body: { note },
  })
}

export function deleteWorkTask(id: string) {
  return apiRequest<unknown>(`/api/work-tasks/${id}`, { method: 'DELETE' })
}

export function workTaskFileUrl(taskId: string, attachmentId: string) {
  return `/api/work-tasks/${taskId}/files/${attachmentId}`
}
