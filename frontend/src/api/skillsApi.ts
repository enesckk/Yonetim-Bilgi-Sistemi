import { apiRequest } from './client'

export interface SkillLevelCount {
  label: string
  count: number
}

export interface SkillCatalogItem {
  id: string
  name: string
  category: number
  categoryLabel: string
  isActive: boolean
  employeeCount: number
  certificateCount: number
  levelBreakdown: SkillLevelCount[]
}

export interface SkillCategoryGroup {
  category: number
  categoryLabel: string
  skills: SkillCatalogItem[]
}

export interface SkillCatalog {
  totalSkills: number
  activeSkills: number
  totalAssignments: number
  employeesWithoutSkills: number
  categories: SkillCategoryGroup[]
}

export interface SkillEmployeeItem {
  id: string
  fullName: string
  unitName?: string | null
  jobTitleName?: string | null
  levelLabel: string
  hasCertificate: boolean
  experienceDuration?: string | null
}

export interface SkillCatalogDetail {
  id: string
  name: string
  category: number
  categoryLabel: string
  isActive: boolean
  employeeCount: number
  certificateCount: number
  levelBreakdown: SkillLevelCount[]
  employees: SkillEmployeeItem[]
}

export interface UpsertSkillPayload {
  name: string
  category: number
  isActive?: boolean
}

export const SKILL_CATEGORIES: { value: number; label: string }[] = [
  { value: 1, label: 'Teknik' },
  { value: 2, label: 'Eğitim / Atölye' },
  { value: 3, label: 'İdari' },
  { value: 4, label: 'İletişim' },
  { value: 5, label: 'Dil' },
]

export async function fetchSkillCatalog(): Promise<SkillCatalog> {
  return apiRequest<SkillCatalog>('/api/skills')
}

export async function fetchSkillCatalogDetail(id: string): Promise<SkillCatalogDetail> {
  return apiRequest<SkillCatalogDetail>(`/api/skills/${id}`)
}

export async function createSkill(payload: UpsertSkillPayload): Promise<{ id: string }> {
  return apiRequest<{ id: string }>('/api/skills', { method: 'POST', body: payload })
}

export async function updateSkill(id: string, payload: UpsertSkillPayload): Promise<void> {
  await apiRequest<unknown>(`/api/skills/${id}`, { method: 'PUT', body: payload })
}

export async function deleteSkill(id: string): Promise<void> {
  await apiRequest<unknown>(`/api/skills/${id}`, { method: 'DELETE' })
}
