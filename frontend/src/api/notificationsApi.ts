import { apiRequest } from './client'

export type NotificationSeverity = 1 | 2 | 3 | 4

export interface AppNotification {
  id: string
  title: string
  body: string
  severity: NotificationSeverity
  category: string
  linkUrl?: string | null
  isRead: boolean
  createdAtUtc: string
  readAtUtc?: string | null
}

export interface NotificationScanResult {
  createdCount: number
  skippedDuplicateCount: number
  summary: string[]
}

export const NOTIFICATION_CATEGORIES = [
  { key: '', label: 'Tümü' },
  { key: 'DataQuality', label: 'Veri kalitesi' },
  { key: 'Certificates', label: 'Sertifikalar' },
  { key: 'Organization', label: 'Organizasyon' },
  { key: 'Staffing', label: 'Kadro' },
  { key: 'Notes', label: 'Notlar' },
  { key: 'Assignments', label: 'Görevler' },
  { key: 'Employees', label: 'Personel' },
  { key: 'Import', label: 'Aktarım' },
  { key: 'Export', label: 'Dışa aktarım' },
  { key: 'System', label: 'Sistem' },
  { key: 'Security', label: 'Güvenlik' },
] as const

export async function fetchNotifications(opts?: {
  unreadOnly?: boolean
  category?: string
  take?: number
}): Promise<AppNotification[]> {
  const qs = new URLSearchParams()
  if (opts?.unreadOnly) qs.set('unreadOnly', 'true')
  if (opts?.category) qs.set('category', opts.category)
  if (opts?.take) qs.set('take', String(opts.take))
  const q = qs.toString()
  return apiRequest<AppNotification[]>(`/api/notifications${q ? `?${q}` : ''}`)
}

export async function fetchUnreadNotificationCount(): Promise<number> {
  const data = await apiRequest<{ count: number }>('/api/notifications/unread-count')
  return data.count
}

export async function markNotificationRead(id: string): Promise<void> {
  await apiRequest<unknown>(`/api/notifications/${id}/read`, { method: 'POST' })
}

export async function markAllNotificationsRead(): Promise<void> {
  await apiRequest<unknown>('/api/notifications/read-all', { method: 'POST' })
}

export async function runNotificationScan(): Promise<NotificationScanResult> {
  return apiRequest<NotificationScanResult>('/api/notifications/scan', { method: 'POST' })
}
