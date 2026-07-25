import { apiRequest } from './client'

export interface DashboardNamedCount {
  name: string
  count: number
}

export interface DashboardCards {
  totalEmployees: number
  activeEmployees: number
  passiveEmployees: number
  totalUnits: number
  totalFacilities: number
  activeFacilities: number
  closedOrRenovationFacilities: number
  civilServantCount: number
  companyStaffCount: number
  instructorCount: number
  technicalCount: number
  libraryStaffCount: number
  auxiliaryCount: number
  incompleteProfileCount: number
  missingSkillsCount: number
  workplaceChangedCount: number
}

export interface DashboardAlertItem {
  code: string
  severity: 'danger' | 'warn' | 'info' | string
  title: string
  description: string
  count: number
  href: string
}

export interface DashboardRecentMovement {
  id: string
  employeeId: string
  employeeName: string
  movementTypeLabel: string
  startDate: string
  summary?: string | null
}

export interface DashboardQuickLink {
  label: string
  href: string
  description?: string | null
}

export interface DashboardAttention {
  unreadNotifications: number
  certificateExpired: number
  certificateExpiringSoon: number
  recentMovements30Days: number
  alerts: DashboardAlertItem[]
  recentMovements: DashboardRecentMovement[]
  quickLinks: DashboardQuickLink[]
}

export interface DashboardSummary {
  cards: DashboardCards
  attention: DashboardAttention
  byUnit: DashboardNamedCount[]
  byEmploymentType: DashboardNamedCount[]
  byDutyCategory: DashboardNamedCount[]
  byEducationLevel: DashboardNamedCount[]
  byServiceYears: DashboardNamedCount[]
}

export async function fetchDashboardSummary(): Promise<DashboardSummary> {
  return apiRequest<DashboardSummary>('/api/dashboard/summary')
}
