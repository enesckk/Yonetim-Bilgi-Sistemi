import { apiRequest } from './client'

export interface NamedCount {
  name: string
  count: number
}

export interface OrgNode {
  id: string
  name: string
  code?: string | null
  type: number
  typeLabel: string
  status: number
  statusLabel: string
  parentId?: string | null
  parentName?: string | null
  description?: string | null
  phone?: string | null
  email?: string | null
  facilityCategoryId?: string | null
  facilityCategoryName?: string | null
  address?: string | null
  capacity?: number | null
  workingHours?: string | null
  managerEmployeeId?: string | null
  managerName?: string | null
  managerDutyName?: string | null
  idealStaffCount?: number | null
  activeEmployeeCount?: number
  /** Eski API yanıtlarıyla geçici geriye uyumluluk. */
  employeeCount?: number
  missingStaffCount?: number
  openedOn?: string | null
  closedOn?: string | null
  updatedAtUtc?: string | null
  dutyBreakdown?: NamedCount[]
  chartPersonnel?: ChartPerson[]
  children: OrgNode[]
}

export interface ChartPerson {
  id: string
  fullName: string
  jobTitleName?: string | null
  dutyName?: string | null
  employmentTypeName?: string | null
}

export interface OrgUnitDetail {
  id: string
  name: string
  code?: string | null
  type: number
  typeLabel: string
  status: number
  statusLabel: string
  parentId?: string | null
  parentName?: string | null
  description?: string | null
  phone?: string | null
  email?: string | null
  facilityCategoryId?: string | null
  facilityCategoryName?: string | null
  address?: string | null
  capacity?: number | null
  workingHours?: string | null
  managerEmployeeId?: string | null
  managerName?: string | null
  idealStaffCount?: number | null
  activeEmployeeCount: number
  missingStaffCount: number
  openedOn?: string | null
  closedOn?: string | null
  updatedAtUtc?: string | null
  dutyDistribution: NamedCount[]
  employmentTypeDistribution: NamedCount[]
  skills: NamedCount[]
  childFacilities: Array<{
    id: string
    name: string
    typeLabel: string
    statusLabel: string
    activeEmployeeCount: number
    idealStaffCount?: number | null
  }>
  personnelByDuty: Array<{
    dutyName: string
    employees: Array<{
      id: string
      fullName: string
      employeeNumber?: string | null
      jobTitleName?: string | null
      statusLabel: string
      isPrimaryDuty: boolean
    }>
  }>
  recentMovements: Array<{
    id: string
    movementTypeLabel: string
    employeeName: string
    startDate: string
    reason?: string | null
  }>
}

export interface OrgFormOptions {
  types: { value: number; label: string }[]
  statuses: { value: number; label: string }[]
  parentCandidates: { id: string; name: string; code?: string | null; type?: number | null }[]
  managers: { id: string; name: string; code?: string | null }[]
  facilityCategories: { id: string; name: string; code?: string | null }[]
}

export interface UpsertOrgUnitPayload {
  name: string
  code?: string | null
  type: number
  status: number
  parentId?: string | null
  description?: string | null
  phone?: string | null
  email?: string | null
  facilityCategoryId?: string | null
  address?: string | null
  capacity?: number | null
  workingHours?: string | null
  idealStaffCount?: number | null
  managerEmployeeId?: string | null
  openedOn?: string | null
  closedOn?: string | null
}

export async function fetchOrganizationTree(): Promise<OrgNode[]> {
  return apiRequest<OrgNode[]>('/api/organization/units/tree')
}

export async function fetchOrganizationUnitDetail(id: string): Promise<OrgUnitDetail> {
  return apiRequest<OrgUnitDetail>(`/api/organization/units/${id}`)
}

export async function fetchOrganizationFormOptions(): Promise<OrgFormOptions> {
  return apiRequest<OrgFormOptions>('/api/organization/units/form-options')
}

export async function createOrganizationUnit(
  payload: UpsertOrgUnitPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>('/api/organization/units', {
    method: 'POST',
    body: payload,
  })
}

export async function updateOrganizationUnit(
  id: string,
  payload: UpsertOrgUnitPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/organization/units/${id}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteOrganizationUnit(id: string): Promise<void> {
  await apiRequest<unknown>(`/api/organization/units/${id}`, {
    method: 'DELETE',
  })
}
