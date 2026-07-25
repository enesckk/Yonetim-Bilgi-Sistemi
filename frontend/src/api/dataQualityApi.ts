import { apiRequest } from './client'

export type DataQualityKind = 'missing' | 'inconsistency'

export interface DataQualityRule {
  code: string
  label: string
  description: string
  kind: DataQualityKind
  affectsCompletionScore: boolean
}

export interface DataQualityIssueStat {
  code: string
  label: string
  kind: DataQualityKind
  count: number
}

export interface DataQualityCompletionBand {
  key: string
  label: string
  minPercent: number
  maxPercent: number
  count: number
}

export interface DataQualityEmployee {
  id: string
  fullName: string
  employeeNumber?: string | null
  unitName?: string | null
  facilityName?: string | null
  status: number
  profileCompletionPercent: number
  issueCodes: string[]
  issueLabels: string[]
  missingCodes: string[]
  inconsistencyCodes: string[]
}

export interface DataQualityReport {
  scopedEmployeeCount: number
  employeesWithIssues: number
  missingIssueEmployeeCount: number
  inconsistencyEmployeeCount: number
  averageCompletionPercent: number
  completionBands: DataQualityCompletionBand[]
  issueStats: DataQualityIssueStat[]
  employees: DataQualityEmployee[]
  rules: DataQualityRule[]
}

export async function fetchDataQualityReport(params?: {
  issueCode?: string
  kind?: DataQualityKind | ''
  completionBand?: string
  maxCompletionPercent?: number
  allStatuses?: boolean
}): Promise<DataQualityReport> {
  const qs = new URLSearchParams()
  if (params?.issueCode) qs.set('issueCode', params.issueCode)
  if (params?.kind) qs.set('kind', params.kind)
  if (params?.completionBand) qs.set('completionBand', params.completionBand)
  if (params?.maxCompletionPercent != null)
    qs.set('maxCompletionPercent', String(params.maxCompletionPercent))
  if (params?.allStatuses) qs.set('allStatuses', 'true')
  const q = qs.toString()
  return apiRequest<DataQualityReport>(`/api/data-quality${q ? `?${q}` : ''}`)
}
