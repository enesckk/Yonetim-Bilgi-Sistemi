import { apiRequest } from './client'
import { invalidateCached } from '@/lib/lookupCache'

export type EventStatus = 1 | 2 | 3 | 4
export type EventRecurrenceFrequency = 0 | 1 | 2

export const EVENT_STATUSES: { value: EventStatus; label: string }[] = [
  { value: 1, label: 'Taslak' },
  { value: 2, label: 'Planlandı' },
  { value: 3, label: 'İptal' },
  { value: 4, label: 'Yapıldı' },
]

export const EVENT_WORK_STATUSES: { value: EventStatus; label: string }[] = [
  { value: 2, label: 'Planlandı' },
  { value: 4, label: 'Yapıldı' },
]

export type EventPhase = 'planned' | 'done' | 'other'

/** Özet ve tesis ekranlarında tarihsiz tam geçmiş çekilmesin. */
export function eventsLookbackFromUtc(months = 18): string {
  const d = new Date()
  d.setUTCMonth(d.getUTCMonth() - months)
  return d.toISOString()
}

export function eventPhase(status: EventStatus, startAtUtc: string): EventPhase {
  if (status === 3 || status === 1) return 'other'
  if (status === 4) return 'done'
  return new Date(startAtUtc).getTime() >= Date.now() ? 'planned' : 'done'
}

export function eventPhaseLabel(status: EventStatus, startAtUtc: string): string {
  const phase = eventPhase(status, startAtUtc)
  if (phase === 'planned') return 'Planlanan'
  if (phase === 'done') return 'Yapılan'
  return status === 3 ? 'İptal' : 'Taslak'
}

export const EVENT_RECURRENCE: { value: EventRecurrenceFrequency; label: string }[] = [
  { value: 0, label: 'Tekrar yok' },
  { value: 1, label: 'Haftalık' },
  { value: 2, label: 'Aylık' },
]

export const EVENT_TRANSITION_LABELS: Partial<Record<EventStatus, string>> = {
  1: 'Taslağa al',
  2: 'Planlandı',
  3: 'İptal et',
  4: 'Yapıldı',
}

export interface EventListItem {
  id: string
  title: string
  status: EventStatus
  statusLabel: string
  startAtUtc: string
  endAtUtc?: string | null
  organizingUnitName?: string | null
  facilityId?: string | null
  facilityName?: string | null
  latitude?: number | null
  longitude?: number | null
  address?: string | null
  expectedAttendees?: number | null
  actualAttendees?: number | null
  attendanceCount?: number | null
  seriesId?: string | null
  recurrenceFrequency?: EventRecurrenceFrequency
  recurrenceLabel?: string | null
  responsibleEmployeeId?: string | null
  responsibleEmployeeName?: string | null
  category?: string | null
  categoryLabel?: string | null
}

export interface EventSettlementLink {
  settlementId: string
  settlementName: string
  officialCode: string
  attendanceCount: number
  uniqueBeneficiaryCount?: number | null
  notes?: string | null
}

export interface EventSettlementInput {
  settlementId: string
  attendanceCount: number
  uniqueBeneficiaryCount?: number | null
  notes?: string | null
}

export interface EventDetail extends EventListItem {
  description?: string | null
  organizingUnitId?: string | null
  facilityId?: string | null
  seriesCount?: number
  allowedTransitions?: EventStatus[]
  settlements?: EventSettlementLink[]
}

export interface EventTimelineItem {
  occurredAtUtc: string
  action: string
  actionLabel: string
  userName?: string | null
  detail?: string | null
}

export interface EventTimelineResult {
  items: EventTimelineItem[]
}

export interface FacilityEventStat {
  facilityId: string
  facilityName: string
  totalEvents: number
  upcomingEvents: number
  publishedEvents: number
}

export interface EventFacilityStatsResult {
  items: FacilityEventStat[]
}

export interface EventImportRowResult {
  rowNumber: number
  success: boolean
  eventId?: string | null
  title?: string | null
  errors: string[]
}

export interface EventImportResult {
  totalRows: number
  successCount: number
  failureCount: number
  rows: EventImportRowResult[]
}

export interface FacilityCoordsImportRow {
  rowNumber: number
  success: boolean
  facilityId?: string | null
  facilityName?: string | null
  errors: string[]
}

export interface FacilityCoordsImportResult {
  totalRows: number
  successCount: number
  failureCount: number
  rows: FacilityCoordsImportRow[]
}

export interface EventWritePayload {
  title: string
  description?: string | null
  startAtUtc: string
  endAtUtc?: string | null
  status: EventStatus
  organizingUnitId?: string | null
  facilityId?: string | null
  latitude?: number | null
  longitude?: number | null
  address?: string | null
  expectedAttendees?: number | null
  actualAttendees?: number | null
  responsibleEmployeeId?: string | null
  recurrenceFrequency?: EventRecurrenceFrequency
  recurrenceOccurrences?: number | null
  allowConflicts?: boolean
  category?: string | null
  settlements?: EventSettlementInput[]
}

export interface EventListResult {
  items: EventListItem[]
  totalCount: number
}

export interface EventConflictItem {
  id: string
  title: string
  status: EventStatus
  statusLabel: string
  startAtUtc: string
  endAtUtc?: string | null
}

export interface EventConflictsResult {
  hasConflicts: boolean
  items: EventConflictItem[]
}

export interface MapPin {
  id: string
  kind: string
  title: string
  latitude: number
  longitude: number
  subtitle?: string | null
  linkPath?: string | null
  startAtUtc?: string | null
  statusLabel?: string | null
  categoryName?: string | null
}

export interface MapPinsResult {
  pins: MapPin[]
  defaultLatitude: number
  defaultLongitude: number
  defaultZoom: number
}

export async function fetchEvents(params?: {
  search?: string
  status?: EventStatus
  fromUtc?: string
  toUtc?: string
  settlementId?: string
  facilityId?: string
}): Promise<EventListResult> {
  const q = new URLSearchParams()
  if (params?.search) q.set('search', params.search)
  if (params?.status) q.set('status', String(params.status))
  if (params?.fromUtc) q.set('fromUtc', params.fromUtc)
  if (params?.toUtc) q.set('toUtc', params.toUtc)
  if (params?.settlementId) q.set('settlementId', params.settlementId)
  if (params?.facilityId) q.set('facilityId', params.facilityId)
  const qs = q.toString()
  return apiRequest<EventListResult>(`/api/events${qs ? `?${qs}` : ''}`)
}

export async function fetchEventById(id: string): Promise<EventDetail> {
  return apiRequest<EventDetail>(`/api/events/${id}`)
}

export async function fetchEventTimeline(id: string): Promise<EventTimelineResult> {
  return apiRequest<EventTimelineResult>(`/api/events/${id}/timeline`)
}

export async function fetchEventFacilityStats(): Promise<EventFacilityStatsResult> {
  return apiRequest<EventFacilityStatsResult>('/api/events/facility-stats')
}

export interface HallBooking {
  id: string
  title: string
  status: EventStatus
  statusLabel: string
  startAtUtc: string
  endAtUtc?: string | null
  expectedAttendees?: number | null
  actualAttendees?: number | null
  attendanceCount?: number | null
}

export interface HallBoardItem {
  facilityId: string
  name: string
  parentName?: string | null
  capacity?: number | null
  group: 'kkm' | 'other' | string
  occupiedNow: boolean
  lastDone?: HallBooking | null
  next?: HallBooking | null
  bookings: HallBooking[]
}

export interface HallBoardResult {
  fromUtc: string
  toUtc: string
  halls: HallBoardItem[]
}

export async function fetchHallBoard(params?: { fromUtc?: string; toUtc?: string }): Promise<HallBoardResult> {
  const q = new URLSearchParams()
  if (params?.fromUtc) q.set('fromUtc', params.fromUtc)
  if (params?.toUtc) q.set('toUtc', params.toUtc)
  const qs = q.toString()
  return apiRequest<HallBoardResult>(`/api/events/hall-board${qs ? `?${qs}` : ''}`)
}

export async function checkEventConflicts(params: {
  facilityId?: string | null
  startAtUtc: string
  endAtUtc?: string | null
  excludeEventId?: string | null
}): Promise<EventConflictsResult> {
  const q = new URLSearchParams()
  if (params.facilityId) q.set('facilityId', params.facilityId)
  q.set('startAtUtc', params.startAtUtc)
  if (params.endAtUtc) q.set('endAtUtc', params.endAtUtc)
  if (params.excludeEventId) q.set('excludeEventId', params.excludeEventId)
  return apiRequest<EventConflictsResult>(`/api/events/conflicts?${q}`)
}

export async function createEvent(payload: EventWritePayload): Promise<{ id: string }> {
  const result = await apiRequest<{ id: string }>('/api/events', { method: 'POST', body: payload })
  invalidateCached('map')
  return result
}

export async function updateEvent(id: string, payload: EventWritePayload): Promise<void> {
  await apiRequest<unknown>(`/api/events/${id}`, { method: 'PUT', body: payload })
  invalidateCached('map')
}

export async function changeEventStatus(
  id: string,
  status: EventStatus,
  allowConflicts = false,
): Promise<EventDetail> {
  const result = await apiRequest<EventDetail>(`/api/events/${id}/status`, {
    method: 'PATCH',
    body: { status, allowConflicts },
  })
  invalidateCached('map')
  return result
}

export async function deleteEvent(id: string): Promise<void> {
  await apiRequest<unknown>(`/api/events/${id}`, { method: 'DELETE' })
  invalidateCached('map')
}

export interface EventNote {
  id: string
  authorUserId: string
  authorName: string
  body: string
  createdAtUtc: string
  sharedRecipientCount: number
}

export async function fetchEventNotes(eventId: string): Promise<EventNote[]> {
  return apiRequest<EventNote[]>(`/api/events/${eventId}/notes`)
}

export async function createEventNote(
  eventId: string,
  body: {
    body: string
    shareToDirectors?: boolean
    shareToUnitManagers?: boolean
    recipientUserIds?: string[]
  },
): Promise<EventNote> {
  return apiRequest<EventNote>(`/api/events/${eventId}/notes`, {
    method: 'POST',
    body,
  })
}

export async function fetchMapPins(params?: {
  kinds?: string
  fromUtc?: string
  toUtc?: string
}): Promise<MapPinsResult> {
  const q = new URLSearchParams()
  if (params?.kinds) q.set('kinds', params.kinds)
  if (params?.fromUtc) q.set('fromUtc', params.fromUtc)
  if (params?.toUtc) q.set('toUtc', params.toUtc)
  const qs = q.toString()
  return apiRequest<MapPinsResult>(`/api/map/pins${qs ? `?${qs}` : ''}`)
}
