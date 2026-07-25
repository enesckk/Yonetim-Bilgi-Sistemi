import { apiRequest } from './client'

export interface CertificateCatalogItem {
  id: string
  name: string
  category: string
  categoryLabel: string
  isActive: boolean
  employeeCount: number
  expiredCount: number
  expiringSoonCount: number
  validCount: number
}

export interface CertificateCategoryGroup {
  category: string
  categoryLabel: string
  certificates: CertificateCatalogItem[]
}

export interface CertificateCatalog {
  totalDefinitions: number
  activeDefinitions: number
  totalAssignments: number
  expiredCount: number
  expiringSoonCount: number
  employeesWithoutCertificates: number
  categories: CertificateCategoryGroup[]
}

export interface CertificateEmployeeItem {
  id: string
  employeeId: string
  fullName: string
  unitName?: string | null
  jobTitleName?: string | null
  issuer?: string | null
  issuedOn?: string | null
  expiresOn?: string | null
  expiryStatus: 'valid' | 'expiring' | 'expired' | 'open' | string
  expiryStatusLabel: string
}

export interface CertificateCatalogDetail {
  id: string
  name: string
  category: string
  categoryLabel: string
  isActive: boolean
  employeeCount: number
  expiredCount: number
  expiringSoonCount: number
  validCount: number
  employees: CertificateEmployeeItem[]
}

export interface UpsertCertificateDefinitionPayload {
  name: string
  category?: string | null
  isActive?: boolean
}

/** Seed + yaygın kategoriler — form seçiminde kullanılır; serbest metin de yazılabilir. */
export const CERTIFICATE_CATEGORIES = ['Zorunlu', 'Mesleki', 'Dil', 'Diğer'] as const

export async function fetchCertificateCatalog(): Promise<CertificateCatalog> {
  return apiRequest<CertificateCatalog>('/api/certificates')
}

export async function fetchCertificateCatalogDetail(
  id: string,
): Promise<CertificateCatalogDetail> {
  return apiRequest<CertificateCatalogDetail>(`/api/certificates/${id}`)
}

export async function createCertificateDefinition(
  payload: UpsertCertificateDefinitionPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>('/api/certificates', { method: 'POST', body: payload })
}

export async function updateCertificateDefinition(
  id: string,
  payload: UpsertCertificateDefinitionPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/certificates/${id}`, { method: 'PUT', body: payload })
}

export async function deleteCertificateDefinition(id: string): Promise<void> {
  await apiRequest<unknown>(`/api/certificates/${id}`, { method: 'DELETE' })
}
