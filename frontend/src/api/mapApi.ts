import { apiRequest } from './client'
import { cachedGet, invalidateCached, LOOKUP_TTL_MS, MAP_TTL_MS, SETTLEMENT_TTL_MS } from '@/lib/lookupCache'
import type { EventStatus } from './eventsApi'

export interface SettlementSummary {
  settlementId: string
  officialCode: string
  name: string
  displayName?: string | null
  settlementType: string
  isRural: boolean
  centroidLat?: number | null
  centroidLng?: number | null
  population?: number | null
  maleCount?: number | null
  femaleCount?: number | null
  childCount?: number | null
  populationYear?: number | null
  populationSource?: string | null
  populationIsOfficial: boolean
  activityCount: number
  attendanceCount: number
  uniqueBeneficiaryCount?: number | null
  hasUniqueBeneficiaries: boolean
  coverageRate?: number | null
  coverageLevel: string
  metricLabel: string
  lastActivityDate?: string | null
  headmanName?: string | null
  headmanPhone?: string | null
  schools?: SettlementSchool[]
  facilities?: SettlementFacility[]
  areas?: SettlementArea[]
}

export interface SettlementFacility {
  id: string
  name: string
  categoryName?: string | null
  status: number
  statusLabel: string
  address?: string | null
  phone?: string | null
  managerName?: string | null
  capacity?: number | null
  workingHours?: string | null
}

export interface SettlementArea {
  id: string
  name: string
  areaType: string
  note?: string | null
  address?: string | null
}

export interface SettlementSchool {
  id: string
  name: string
  schoolType: string
  studentCount?: number | null
  principalName?: string | null
  principalPhone?: string | null
}

export interface SettlementLookup {
  id: string
  officialCode: string
  name: string
  isRural: boolean
  headmanName?: string | null
  headmanPhone?: string | null
}

export async function fetchSettlementSummaries(params?: {
  fromUtc?: string
  toUtc?: string
  category?: string
  status?: EventStatus
  search?: string
}): Promise<{ items: SettlementSummary[] }> {
  const q = new URLSearchParams()
  if (params?.fromUtc) q.set('fromUtc', params.fromUtc)
  if (params?.toUtc) q.set('toUtc', params.toUtc)
  if (params?.category) q.set('category', params.category)
  if (params?.status) q.set('status', String(params.status))
  if (params?.search) q.set('search', params.search)
  const qs = q.toString()
  const path = `/api/map/settlements/summary${qs ? `?${qs}` : ''}`
  return cachedGet(`map:summary:${qs}`, MAP_TTL_MS, () =>
    apiRequest<{ items: SettlementSummary[] }>(path),
  )
}

export async function fetchSettlements(search?: string): Promise<SettlementLookup[]> {
  const term = search?.trim() ?? ''
  const q = term ? `?search=${encodeURIComponent(term)}` : ''
  const loader = () => apiRequest<SettlementLookup[]>(`/api/settlements${q}`)
  if (!term) return cachedGet('settlements:all', SETTLEMENT_TTL_MS, loader)
  return cachedGet(`settlements:q:${term.toLocaleLowerCase('tr-TR')}`, LOOKUP_TTL_MS, loader)
}

export async function fetchSettlementDetail(key: string): Promise<SettlementSummary> {
  return apiRequest<SettlementSummary>(`/api/settlements/${encodeURIComponent(key)}`)
}

export async function upsertSettlementPopulation(
  id: string,
  body: {
    year: number
    population: number
    maleCount?: number | null
    femaleCount?: number | null
    childCount?: number | null
    source: string
    sourceReference?: string
    isOfficial: boolean
  },
): Promise<void> {
  await apiRequest<unknown>(`/api/settlements/${id}/population`, { method: 'PUT', body })
  invalidateCached('map')
  invalidateCached('settlements')
}

export async function updateSettlementProfile(
  id: string,
  body: { headmanName?: string | null; headmanPhone?: string | null },
): Promise<void> {
  await apiRequest<unknown>(`/api/settlements/${id}/profile`, { method: 'PUT', body })
  invalidateCached('map')
  invalidateCached('settlements')
}

export async function upsertSettlementSchool(
  settlementId: string,
  body: {
    id?: string | null
    name: string
    schoolType: string
    studentCount?: number | null
    principalName?: string | null
    principalPhone?: string | null
  },
): Promise<string> {
  const id = await apiRequest<string>(`/api/settlements/${settlementId}/schools`, { method: 'POST', body })
  invalidateCached('map')
  return id
}

export async function deleteSettlementSchool(settlementId: string, schoolId: string): Promise<void> {
  await apiRequest<unknown>(`/api/settlements/${settlementId}/schools/${schoolId}`, { method: 'DELETE' })
  invalidateCached('map')
}

export async function upsertSettlementArea(
  settlementId: string,
  body: {
    id?: string | null
    name: string
    areaType: string
    note?: string | null
    address?: string | null
  },
): Promise<string> {
  const id = await apiRequest<string>(`/api/settlements/${settlementId}/areas`, { method: 'POST', body })
  invalidateCached('map')
  return id
}

export async function deleteSettlementArea(settlementId: string, areaId: string): Promise<void> {
  await apiRequest<unknown>(`/api/settlements/${settlementId}/areas/${areaId}`, { method: 'DELETE' })
  invalidateCached('map')
}
