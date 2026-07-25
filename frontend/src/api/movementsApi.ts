import { apiDownloadFile, apiRequest } from './client'

export interface MovementTypeOption {
  value: number
  label: string
}

export interface MovementTypeStat {
  movementType: number
  label: string
  count: number
}

export interface MovementListItem {
  id: string
  employeeId: string
  employeeName: string
  employeeNumber?: string | null
  currentUnitName?: string | null
  movementType: number
  movementTypeLabel: string
  startDate: string
  endDate?: string | null
  oldUnitName?: string | null
  newUnitName?: string | null
  oldFacilityName?: string | null
  newFacilityName?: string | null
  oldJobTitleName?: string | null
  newJobTitleName?: string | null
  oldJobDutyName?: string | null
  newJobDutyName?: string | null
  reason?: string | null
  description?: string | null
  approvedBy?: string | null
  createdBy?: string | null
  createdAtUtc: string
}

export interface MovementListResult {
  totalCount: number
  returnedCount: number
  typeStats: MovementTypeStat[]
  items: MovementListItem[]
  movementTypes: MovementTypeOption[]
}

export interface MovementListParams {
  search?: string
  unitId?: string
  facilityId?: string
  movementType?: number
  from?: string
  to?: string
  take?: number
}

function toQuery(params: MovementListParams): string {
  const q = new URLSearchParams()
  if (params.search?.trim()) q.set('search', params.search.trim())
  if (params.unitId) q.set('unitId', params.unitId)
  if (params.facilityId) q.set('facilityId', params.facilityId)
  if (params.movementType !== undefined) q.set('movementType', String(params.movementType))
  if (params.from) q.set('from', params.from)
  if (params.to) q.set('to', params.to)
  if (params.take) q.set('take', String(params.take))
  const s = q.toString()
  return s ? `?${s}` : ''
}

export async function fetchMovementList(params: MovementListParams = {}): Promise<MovementListResult> {
  return apiRequest<MovementListResult>(`/api/movements${toQuery(params)}`)
}

export async function downloadMovementsExcel(params: MovementListParams = {}): Promise<void> {
  await apiDownloadFile(`/api/movements/excel${toQuery(params)}`)
}
