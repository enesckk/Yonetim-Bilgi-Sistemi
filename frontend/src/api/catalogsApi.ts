import { apiRequest } from './client'

export interface CatalogItem {
  id: string
  name: string
  code?: string | null
  isActive: boolean
  usageCount: number
  category?: number | null
  categoryLabel?: string | null
  sortOrder?: number | null
}

export interface CatalogGroup {
  category: number
  categoryLabel: string
  items: CatalogItem[]
}

export interface CatalogList {
  totalCount: number
  activeCount: number
  inUseCount: number
  items: CatalogItem[]
  groups: CatalogGroup[]
}

export interface CatalogOption {
  value: number
  label: string
}

export interface CatalogsOverview {
  jobDutyCount: number
  jobTitleCount: number
  employmentTypeCount: number
  facilityCategoryCount: number
  dutyCategories: CatalogOption[]
}

export type CatalogKind = 'job-duties' | 'job-titles' | 'employment-types' | 'facility-categories'

export async function fetchCatalogsOverview(): Promise<CatalogsOverview> {
  return apiRequest<CatalogsOverview>('/api/catalogs/overview')
}

export async function fetchCatalogList(kind: CatalogKind): Promise<CatalogList> {
  return apiRequest<CatalogList>(`/api/catalogs/${kind}`)
}

export async function createCatalogItem(
  kind: CatalogKind,
  body: Record<string, unknown>,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/catalogs/${kind}`, {
    method: 'POST',
    body,
  })
}

export async function updateCatalogItem(
  kind: CatalogKind,
  id: string,
  body: Record<string, unknown>,
): Promise<void> {
  await apiRequest<unknown>(`/api/catalogs/${kind}/${id}`, {
    method: 'PUT',
    body,
  })
}

export async function deleteCatalogItem(kind: CatalogKind, id: string): Promise<void> {
  await apiRequest<unknown>(`/api/catalogs/${kind}/${id}`, { method: 'DELETE' })
}
