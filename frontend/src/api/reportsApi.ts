import { apiDownloadFile, apiRequest } from './client'
import type { EmployeeListQuery } from './employeesApi'

export interface ReportsSummary {
  totalEmployees: number
  activeEmployees: number
  passiveEmployees: number
  onLeaveEmployees: number
  withSpecialCondition: number
  organizationUnitCount: number
  byStatus: Array<{
    status: number
    statusLabel: string
    count: number
  }>
}

export type EmployeeExportFilters = Omit<
  EmployeeListQuery,
  'page' | 'pageSize' | 'sortBy' | 'sortDesc'
>

function toExportParams(filters?: EmployeeExportFilters): string {
  const params = new URLSearchParams()
  if (!filters) return ''

  if (filters.search) params.set('search', filters.search)
  if (filters.unitId) params.set('unitId', filters.unitId)
  if (filters.includeSubUnits) params.set('includeSubUnits', 'true')
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.employmentTypeId) params.set('employmentTypeId', filters.employmentTypeId)
  if (filters.jobTitleId) params.set('jobTitleId', filters.jobTitleId)
  if (filters.jobDutyId) params.set('jobDutyId', filters.jobDutyId)
  if (filters.dutyCategory) params.set('dutyCategory', String(filters.dutyCategory))
  if (filters.educationLevel) params.set('educationLevel', String(filters.educationLevel))
  if (filters.skillId) params.set('skillId', filters.skillId)
  if (filters.status) params.set('status', String(filters.status))
  if (filters.incompleteProfileOnly) params.set('incompleteProfileOnly', 'true')
  if (filters.missingSkillsOnly) params.set('missingSkillsOnly', 'true')
  if (filters.missingPhoneOnly) params.set('missingPhoneOnly', 'true')
  if (filters.missingFacilityOnly) params.set('missingFacilityOnly', 'true')
  if (filters.hasSpecialConditionOnly) params.set('hasSpecialConditionOnly', 'true')
  if (filters.hireYearFrom) params.set('hireYearFrom', String(filters.hireYearFrom))
  if (filters.hireYearTo) params.set('hireYearTo', String(filters.hireYearTo))
  if (filters.universityContains) params.set('universityContains', filters.universityContains)
  if (filters.graduationYearFrom) params.set('graduationYearFrom', String(filters.graduationYearFrom))
  if (filters.graduationYearTo) params.set('graduationYearTo', String(filters.graduationYearTo))
  if (filters.hasNotesOnly) params.set('hasNotesOnly', 'true')
  if (filters.missingCertificatesOnly) params.set('missingCertificatesOnly', 'true')
  if (filters.expiredCertificateOnly) params.set('expiredCertificateOnly', 'true')

  const qs = params.toString()
  return qs ? `?${qs}` : ''
}

export async function fetchReportsSummary(): Promise<ReportsSummary> {
  return apiRequest<ReportsSummary>('/api/reports/summary')
}

// ---- Rapor oluşturucu ----

export interface ReportColumn {
  key: string
  label: string
  group: string
}

export interface ReportBuildPayload extends EmployeeExportFilters {
  title?: string
  columns: string[]
  groupBy?: string | null
  orderByColumn?: string | null
  orderByDesc?: boolean
  hasCertificatesOnly?: boolean
  hasMovementsOnly?: boolean
  /** yyyy-MM-dd */
  movementFrom?: string | null
  movementTo?: string | null
}

export interface ReportResult {
  title: string
  generatedBy: string
  generatedAtUtc: string
  totalCount: number
  columns: ReportColumn[]
  rows: (string | null)[][]
  appliedFilters: string[]
  groupByKey?: string | null
  groupByLabel?: string | null
}

export async function fetchReportColumns(): Promise<ReportColumn[]> {
  return apiRequest<ReportColumn[]>('/api/reports/columns')
}

export async function buildReport(payload: ReportBuildPayload): Promise<ReportResult> {
  return apiRequest<ReportResult>('/api/reports/build', { method: 'POST', body: payload })
}

export async function downloadReportExcel(payload: ReportBuildPayload): Promise<void> {
  await apiDownloadFile('/api/reports/build/excel', { body: payload })
}

export async function downloadReportPdf(payload: ReportBuildPayload): Promise<void> {
  await apiDownloadFile('/api/reports/build/pdf', { body: payload })
}

export async function downloadEmployeesExcel(filters?: EmployeeExportFilters): Promise<void> {
  await apiDownloadFile(`/api/reports/employees/excel${toExportParams(filters)}`)
}

export async function downloadEmployeesPdf(filters?: EmployeeExportFilters): Promise<void> {
  await apiDownloadFile(`/api/reports/employees/pdf${toExportParams(filters)}`)
}
